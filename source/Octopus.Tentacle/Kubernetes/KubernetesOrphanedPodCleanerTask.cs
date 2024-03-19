using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesOrphanedPodCleanerTask
    {
        void Start();
        void Stop();
    }

    public class KubernetesOrphanedPodCleanerTask : IKubernetesOrphanedPodCleanerTask, IDisposable
    {
        readonly IKubernetesOrphanedPodCleaner podCleaner;
        readonly ISystemLog log;
        readonly CancellationTokenSource cancellationTokenSource = new ();

        readonly object @lock = new();

        Task? cleanerTask;

        public KubernetesOrphanedPodCleanerTask(IKubernetesOrphanedPodCleaner podCleaner, ISystemLog log)
        {
            this.podCleaner = podCleaner;
            this.log = log;
        }

        public void Start()
        {
            lock (@lock)
            {
                if (cleanerTask is not null)
                {
                    log.Error("Kubernetes Orphaned Pod Cleaner task already running.");
                    return;
                }

                log.Info("Starting Kubernetes Orphaned Pod Cleaner");
                cleanerTask = Task.Run(() => podCleaner.StartAsync(cancellationTokenSource.Token));
            }
        }

        public void Stop()
        {
            lock (@lock)
            {
                if (cleanerTask is null) return;

                try
                {
                    log.Info("Stopping Kubernetes Orphaned Pod Cleaner");
                    cancellationTokenSource.Cancel();

                    // give the cleaner 30 to gracefully shutdown
                    cleanerTask.Wait(TimeSpan.FromSeconds(30));
                }
                catch (Exception e)
                {
                    log.Error(e, "Could not stop Kubernetes Orphaned Pod Cleaner");
                }
                finally
                {
                    log.Info("Stopped Kubernetes Orphaned Pod Cleaner");
                    cleanerTask = null;
                }
            }
        }

        public void Dispose()
        {
            log.Info("Disposing of Kubernetes Orphaned Pod Cleaner");
            Stop();
            cancellationTokenSource.Dispose();
        }
    }
}