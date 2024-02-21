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

                monitorTask = jobMonitor.StartAsync(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            lock (LockObj)
            {

                if (monitorTask is null) return;

                try
                {
                    cancellationTokenSource.Cancel();

                    monitorTask.Wait();
                }
                catch (Exception e)
                {
                    log.Error(e, "Could not stop Kubernetes Job Monitor cleaner");
                }
                finally
                {
                    monitorTask = null;
                }
            }
        }

        public void Dispose()
        {
            Stop();
            cancellationTokenSource.Dispose();
        }
    }
}