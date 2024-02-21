using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesJobMonitor
    {
        Task StartAsync(CancellationToken token);
    }

    public interface IKubernetesJobStatusProvider
    {
        JobStatus? TryGetJobStatus(ScriptTicket scriptTicket);
    }

    public class KubernetesJobMonitor : IKubernetesJobMonitor, IKubernetesJobStatusProvider
    {
        readonly IKubernetesJobService jobService;
        readonly IKubernetesPodService podService;
        readonly ISystemLog log;
        readonly Dictionary<ScriptTicket, JobStatus> jobStatusLookup = new();

        public KubernetesJobMonitor(IKubernetesJobService jobService, IKubernetesPodService podService, ISystemLog log)
        {
            this.jobService = jobService;
            this.podService = podService;
            this.log = log;
        }

        async Task IKubernetesJobMonitor.StartAsync(CancellationToken cancellationToken)
        {
            //initially load all the jobs and their status's
            await InitialLoadAsync(cancellationToken);

            await jobService.WatchAllJobsAsync(async (type, job) =>
                {
                    await Task.CompletedTask;

                    var scriptTicket = job.GetScriptTicket();

                    switch (type)
                    {
                        case WatchEventType.Added or WatchEventType.Modified:
                        {
                            if (!jobStatusLookup.TryGetValue(scriptTicket, out var status))
                            {
                                status = new JobStatus(job.GetScriptTicket());
                                jobStatusLookup[scriptTicket] = status;
                            }

                            await status.UpdateAsync(job, podService, cancellationToken);

                            break;
                        }
                        case WatchEventType.Deleted:
                            //if the job is deleted, remove it
                            jobStatusLookup.Remove(scriptTicket);
                            break;
                        default:
                            log.Warn($"Received watch event type {type} for job {job.Name()}. Ignoring");
                            break;
                    }
                }, ex =>
                {
                    log.Error(ex, "An unhandled error occured in monitoring the jobs");
                }, cancellationToken
            );
        }

        JobStatus? IKubernetesJobStatusProvider.TryGetJobStatus(ScriptTicket scriptTicket)
            => jobStatusLookup.TryGetValue(scriptTicket, out var status) ? status : null;

        async Task InitialLoadAsync(CancellationToken cancellationToken)
        {
            var allJobs = await jobService.ListAllJobsAsync(cancellationToken);
            foreach (var job in allJobs.Items)
            {
                var status = new JobStatus(job.GetScriptTicket());
                await status.UpdateAsync(job, podService, cancellationToken);

                jobStatusLookup[status.ScriptTicket] = status;
            }
        }
    }

    public class JobStatus
    {
        public ScriptTicket ScriptTicket { get; }

        public bool Success { get; private set; }

        public bool Failed { get; private set; }

        public int? ExitCode { get; private set; }

        public JobStatus(ScriptTicket ticket)
        {
            ScriptTicket = ticket;
        }

        public async Task UpdateAsync(V1Job job, IKubernetesPodService podService, CancellationToken cancellationToken)
        {
            var firstCondition = job.Status?.Conditions?.FirstOrDefault();
            switch (firstCondition)
            {
                case { Status: "True", Type: "Complete" }:
                    Success = true;
                    Failed = false;
                    ExitCode = 0;
                    break;
                case { Status: "True", Type: "Failed" }:
                    Success = false;
                    Failed = true;

                    var pod = await podService.TryGetPodForJob(ScriptTicket, cancellationToken);

                    //find the status for the container
                    //we we can't determine the exit code from the pod container, just return 1
                    ExitCode = pod?.Status?.ContainerStatuses?.FirstOrDefault()?.State?.Terminated?.ExitCode ?? 1;

                    break;
            }
        }
    }

    public static class V1JobExtensions
    {
        public static ScriptTicket GetScriptTicket(this V1Job job) => new(job.GetLabel(OctopusLabels.ScriptTicketId));
    }
}