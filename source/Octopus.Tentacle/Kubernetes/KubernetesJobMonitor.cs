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
            while (!cancellationToken.IsCancellationRequested)
            {
                //initially load all the jobs and their status's
                var initialResourceVersion = await InitialLoadAsync(cancellationToken);

                //we start the watch from the resource version we initially loaded. This means we receive w
                await jobService.WatchAllJobsAsync(initialResourceVersion,async (type, job) =>
                    {
                        try
                        {
                            log.Verbose($"Received {type} event for job {job.Name()}");

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
                                    log.Verbose($"Updated job {job.Name()} status. {status}");

                                    break;
                                }
                                case WatchEventType.Deleted:
                                    log.Verbose($"Removed {type} job {job.Name()} status");

                                    //if the job is deleted, remove it
                                    jobStatusLookup.Remove(scriptTicket);
                                    break;
                                default:
                                    log.Warn($"Received watch event type {type} for job {job.Name()}. Ignoring as we don't need it");
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            log.Error(e, $"Failed to process event {type} for job {job.Name()}.");
                        }
                    }, ex =>
                    {
                        log.Error(ex, "An unhandled error occured in monitoring the jobs");
                    }, cancellationToken
                );
            }
        }

        async Task<string> InitialLoadAsync(CancellationToken cancellationToken)
        {
            log.Verbose("Preloading job statuses");
            //clear the status'
            jobStatusLookup.Clear();

            var allJobs = await jobService.ListAllJobsAsync(cancellationToken);
            foreach (var job in allJobs.Items)
            {
                var status = new JobStatus(job.GetScriptTicket());
                await status.UpdateAsync(job, podService, cancellationToken);

                log.Verbose($"Preloaded job {job.Name()}. {status}");
                jobStatusLookup[status.ScriptTicket] = status;
            }

            log.Verbose($"Preloaded {allJobs.Items.Count} job statuses. ResourceVersion: {allJobs.ResourceVersion()}");

            //this is the resource version for the list. We use this to start the watch at this particular point
            return allJobs.ResourceVersion();
        }

        JobStatus? IKubernetesJobStatusProvider.TryGetJobStatus(ScriptTicket scriptTicket)
            => jobStatusLookup.TryGetValue(scriptTicket, out var status) ? status : null;
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

        public override string ToString()
            => $"ScriptTicket: {ScriptTicket}, Success: {Success}, Failed: {Failed}, ExitCode: {ExitCode}";
    }

    public static class V1JobExtensions
    {
        public static ScriptTicket GetScriptTicket(this V1Job job) => new(job.GetLabel(OctopusLabels.ScriptTicketId));
    }
}