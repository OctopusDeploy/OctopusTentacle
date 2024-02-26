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
    public class PodLogMonitor
    {
        public delegate PodLogMonitor Factory(V1Pod pod);

        readonly IKubernetesPodService podService;
        readonly ISystemLog log;
        Task? backgroundTask;
        CancellationTokenSource? cancellationTokenSource;

        List<PodLogLine> logLines = new();

        readonly object logLock = new();
        readonly string podName;
        readonly string containerName;

        long currentSequenceNumber;

        public PodLogMonitor(V1Pod pod, IKubernetesPodService podService, ISystemLog log)
        {
            this.podService = podService;
            this.log = log;
            podName = pod.Name();
            containerName = pod.Spec.Containers.First().Name;
        }

        public void StartMonitoring(CancellationToken cancellationToken)
        {
            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            backgroundTask = Task.Run(() => WatchPodLogsAsync(cancellationTokenSource.Token), cancellationToken);
        }

        public void StopMonitoring()
        {
            if (backgroundTask is null || cancellationTokenSource is null)
                return;

            cancellationTokenSource.Cancel();
            backgroundTask.Wait(TimeSpan.FromSeconds(30));
        }

        public (long newSequence, List<PodLogLine>) GetLogs(long lastLogSequence)
        {
            lock (logLock)
            {
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
                    continue;

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
                    logLines.Add(new PodLogLine(occured, source, parts[2]));
                }
            }
        }
    }

    public record PodLogLine(DateTimeOffset Occurred, ProcessOutputSource Source, string Message);
}