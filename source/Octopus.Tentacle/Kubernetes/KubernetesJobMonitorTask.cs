using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesJobMonitorTask
    {
        void Start();
        void Stop();
    }

    public class KubernetesJobMonitorTask: IKubernetesJobMonitorTask, IDisposable
    {
        readonly IKubernetesJobMonitor jobMonitor;
        readonly ISystemLog log;
        readonly CancellationTokenSource cancellationTokenSource = new ();

        static readonly object LockObj = new();

        Task? monitorTask;

        public KubernetesJobMonitorTask(IKubernetesJobMonitor jobMonitor, ISystemLog log)
        {
            this.jobMonitor = jobMonitor;
            this.log = log;
        }

        public void Start()
        {
            lock (LockObj)
            {
                if (monitorTask is not null)
                {
                    log.Error("Kubernetes Job Monitor task already running.");
                    return;
                }

                log.Info("Starting Kubernetes Job Monitor");
                monitorTask = Task.Run(() => jobMonitor.StartAsync(cancellationTokenSource.Token));
            }
        }

        public void Stop()
        {
            lock (LockObj)
            {
                if (monitorTask is null) return;

                try
                {
                    log.Info("Stopping Kubernetes Job Monitor");
                    cancellationTokenSource.Cancel();

                    monitorTask.Wait();
                }
                catch (Exception e)
                {
                    log.Error(e, "Could not stop Kubernetes Job Monitor cleaner");
                }
                finally
                {
                    log.Info("Stopped Kubernetes Job Monitor");
                    monitorTask = null;
                }
            }
        }

        public void Dispose()
        {
            log.Info("Disposing of Kubernetes Job Monitor");
            Stop();
            cancellationTokenSource.Dispose();
        }
    }
}