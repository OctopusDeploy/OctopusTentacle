using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesPodLogMonitor
    {
        public delegate KubernetesPodLogMonitor Factory(V1Pod pod, Action<TrackedPodState, int> onScriptFinished);

        readonly Action<TrackedPodState, int> onScriptFinished;
        readonly IKubernetesPodService podService;
        readonly ISystemLog log;
        Task? backgroundTask;
        CancellationTokenSource? cancellationTokenSource;

        readonly List<PodLogLine> logLines = new();

        readonly object logLock = new();
        readonly string podName;
        readonly string containerName;

        long currentSequenceNumber;

        public KubernetesPodLogMonitor(V1Pod pod, Action<TrackedPodState, int> onScriptFinished, IKubernetesPodService podService, ISystemLog log)
        {
            this.onScriptFinished = onScriptFinished;
            this.podService = podService;
            this.log = log;
            podName = pod.Name();
            containerName = pod.Spec.Containers.First().Name;
        }

        public void StartMonitoring(CancellationToken cancellationToken)
        {
            //We are already running, so don't bother trying again
            if (backgroundTask is not null)
                return;

            log.Verbose($"Starting log monitoring for pod {podName}");
            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            backgroundTask = Task.Run(async () => await WatchPodLogsAsync(cancellationTokenSource.Token).ConfigureAwait(false), cancellationToken);
        }


        public void StopMonitoring()
        {
            if (backgroundTask is null || cancellationTokenSource is null)
                return;

            log.Verbose($"Stopping log monitoring for pod {podName}");
            try
            {
                cancellationTokenSource.Cancel();
                backgroundTask.Wait(TimeSpan.FromSeconds(30));
            }
            catch (TaskCanceledException)
            {
                //ignore these exceptions
            }
            catch (Exception e)
            {
                log.Verbose(e, $"Failed to stop log monitoring for pod {podName}");
            }
            finally
            {
                log.Verbose($"Stopped log monitoring for pod {podName}");
                backgroundTask = null;
            }
        }

        public (long newSequence, List<PodLogLine>) GetLogs(long lastLogSequence)
        {
            log.Verbose($"Get logs for pod {podName}. Sequence {lastLogSequence}");

            lock (logLock)
            {
                //no lines, nothing to return
                if (logLines.Count == 0)
                    return (lastLogSequence, new List<PodLogLine>());

                // we determine how many lines to retrieve
                // this number is how many tail lines we need to return
                var linesToGet = (logLines.Count + currentSequenceNumber) - lastLogSequence;

                var newSequence = lastLogSequence + linesToGet;

                // shortcut if we are return the entire list
                var linesToReturn = linesToGet == logLines.Count
                    ? logLines.ToList()
                    : TakeLast(logLines, linesToGet).ToList();

                //clear the current log lines list
                logLines.Clear();
                //update our sequence number
                currentSequenceNumber = newSequence;

                log.Verbose($"Got logs for pod {podName}. New sequence {newSequence}, Log lines: {linesToReturn.Count}");
                return (newSequence, linesToReturn);

                // Because we are mixing longs and int's, this becomes the easiest way to do this.
                IEnumerable<PodLogLine> TakeLast(IReadOnlyList<PodLogLine> lines, long count)
                {
                    var startingIndex = lines.Count - count;
                    for (var i = 0; i < lines.Count; i++)
                    {
                        if (i <= startingIndex)
                        {
                            continue;
                        }

                        yield return lines[i];
                    }
                }
            }
        }

        async Task WatchPodLogsAsync(CancellationToken cancellationToken)
        {
            await foreach (var logLine in podService.StreamPodLogs(podName, containerName, cancellationToken))
            {
                if (logLine is null)
                    continue;

                var parts = logLine.Split('|');
                if (parts.Length != 3)
                {
                    log.Verbose($"Received pod log in the wrong format: '{logLine}'");
                    continue;
                }

                var message = parts[2];

                // //if this is the end of m
                // if (message.StartsWith(KubernetesConfig.EndOfScriptControlMessage, StringComparison.OrdinalIgnoreCase))
                // {
                //     //the second value is always the exit code
                //     var exitCode = int.Parse(message.Split(new[] { "<<>>" }, StringSplitOptions.None)[1]);
                //
                //     onScriptFinished(exitCode == 0 ? TrackedPodState.Succeeded : TrackedPodState.Failed, exitCode);
                //     break;
                // }

                var occured = DateTimeOffset.Parse(parts[0]);
                var source = parts[1] switch
                {
                    "stdout" => ProcessOutputSource.StdOut,
                    "stderr" => ProcessOutputSource.StdErr,
                    _ => throw new InvalidOperationException($"Unknown source {parts[1]}")
                };

                lock (logLock)
                {
                    //we are making a bold assumption that the pod logs are coming in sequential order
                    logLines.Add(new PodLogLine(occured, source, message));
                }
            }
        }
    }

    public record PodLogLine(DateTimeOffset Occurred, ProcessOutputSource Source, string Message);
}