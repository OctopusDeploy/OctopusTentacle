using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Options;
using Octopus.Pipelines.Runner.Gateway;

namespace Octopus.Pipelines.Runner.Kubernetes;

/// <summary>
/// One list-and-watch per runner over its own job pods (by label), instead of one watch per pod.
/// At thousands of pods per cell, per-pod watches would multiply API server load.
/// </summary>
public sealed class PodWatcher(IKubernetes client, IOptions<KubernetesBackendOptions> options, IOptions<GatewayOptions> gateway, ILogger<PodWatcher> logger) : BackgroundService
{
    public const string ContainerName = "job";
    public const string RunnerLabel = "poc.octopus.com/runner-id";

    readonly ConcurrentDictionary<string, PodTracker> trackers = new();

    public PodTracker Track(string podName) => trackers.GetOrAdd(podName, _ => new PodTracker());

    public void Untrack(string podName) => trackers.TryRemove(podName, out _);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runner = options.Value;
        var ns = runner.Namespace;
        var selector = $"{RunnerLabel}={gateway.Value.RunnerId}";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // List first so nothing that changed while the watch was down is missed.
                var list = await client.CoreV1.ListNamespacedPodAsync(ns, labelSelector: selector, cancellationToken: stoppingToken);
                foreach (var pod in list.Items)
                {
                    Observe(WatchEventType.Modified, pod);
                }

                var resourceVersion = list.Metadata.ResourceVersion;
                await foreach (var (type, pod) in client.CoreV1.WatchListNamespacedPodAsync(
                                   ns,
                                   labelSelector: selector,
                                   resourceVersion: resourceVersion,
                                   allowWatchBookmarks: true,
                                   timeoutSeconds: 300,
                                   onError: ex => logger.LogWarning(ex, "Pod watch error"),
                                   cancellationToken: stoppingToken))
                {
                    if (type == WatchEventType.Error)
                    {
                        // Usually "resource version too old": relist.
                        break;
                    }

                    if (type != WatchEventType.Bookmark)
                    {
                        Observe(type, pod);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is HttpOperationException or HttpRequestException or IOException)
            {
                logger.LogWarning(ex, "Pod watch failed; relisting");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    void Observe(WatchEventType type, V1Pod pod)
    {
        if (pod.Metadata?.Name is { } name && trackers.TryGetValue(name, out var tracker))
        {
            tracker.Observe(type, pod);
        }
    }
}

/// <summary>What the watch has learned about one job pod.</summary>
public sealed class PodTracker
{
    readonly TaskCompletionSource<DateTimeOffset> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource<int> terminated = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the job container is running or has already finished.</summary>
    public Task<DateTimeOffset> Started => started.Task;

    /// <summary>Completes with the container exit code.</summary>
    public Task<int> Terminated => terminated.Task;

    /// <summary>Called with true when the pod is unschedulable (waiting for a node), false once it's scheduled. Only on changes.</summary>
    public Action<bool>? PlacementChanged { get; set; }

    bool? waitingForNode;

    internal void Observe(WatchEventType type, V1Pod pod)
    {
        var scheduled = pod.Status?.Conditions?.FirstOrDefault(c => c.Type == "PodScheduled");
        bool? unschedulable = scheduled switch
        {
            { Status: "True" } => false,
            { Status: "False", Reason: "Unschedulable" } => true,
            _ => null,
        };
        if (unschedulable is { } now && now != waitingForNode)
        {
            waitingForNode = now;
            PlacementChanged?.Invoke(now);
        }

        var status = pod.Status?.ContainerStatuses?.FirstOrDefault(c => c.Name == PodWatcher.ContainerName);
        if (status?.State?.Running is { } running)
        {
            started.TrySetResult(ToOffset(running.StartedAt));
        }

        if (status?.State?.Terminated is { } done)
        {
            started.TrySetResult(ToOffset(done.StartedAt ?? done.FinishedAt));
            terminated.TrySetResult(done.ExitCode);
            return;
        }

        if (status?.State?.Waiting is { Reason: "ErrImagePull" or "ImagePullBackOff" or "InvalidImageName" or "CreateContainerConfigError" } waiting)
        {
            Fail($"{waiting.Reason}: {waiting.Message}");
            return;
        }

        if (pod.Status?.Phase == "Failed")
        {
            Fail($"Pod failed: {pod.Status.Reason} {pod.Status.Message}".Trim());
        }
        else if (type == WatchEventType.Deleted)
        {
            Fail("Pod was deleted before the job finished.");
        }
    }

    void Fail(string reason)
    {
        var ex = new InvalidOperationException(reason);
        started.TrySetException(ex);
        terminated.TrySetException(ex);
    }

    static DateTimeOffset ToOffset(DateTime? at) =>
        at is { } value ? new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)) : DateTimeOffset.UtcNow;
}
