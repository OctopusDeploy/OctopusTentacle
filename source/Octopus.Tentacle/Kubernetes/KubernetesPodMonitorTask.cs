using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodMonitorTask
    {
        void Start();
        void Stop();
    }

    public class KubernetesPodMonitorTask: IKubernetesPodMonitorTask, IDisposable
    {
        readonly IKubernetesPodMonitor podMonitor;
        readonly ISystemLog log;
        readonly CancellationTokenSource cancellationTokenSource = new ();

        readonly object LockObj = new();

        Task? monitorTask;

        public KubernetesPodMonitorTask(IKubernetesPodMonitor podMonitor, ISystemLog log)
        {
            this.podMonitor = podMonitor;
            this.log = log;
        }

        public void Start()
        {
            lock (LockObj)
            {
                if (monitorTask is not null)
                {
                    log.Error("Kubernetes Pod Monitor task already running.");
                    return;
                }

                log.Info("Starting Kubernetes Pod Monitor");
                monitorTask = Task.Run(async () => await podMonitor.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false));
            }
        }

        public void Stop()
        {
            lock (LockObj)
            {
                if (monitorTask is null) return;

                try
                {
                    log.Info("Stopping Kubernetes Pod Monitor");
                    cancellationTokenSource.Cancel();

                    // give the monitor 30 to gracefully shutdown
                    monitorTask.Wait(TimeSpan.FromSeconds(30));
                }
                catch (Exception e)
                {
                    log.Error(e, "Could not stop Kubernetes Pod Monitor");
                }
                finally
                {
                    log.Info("Stopped Kubernetes Pod Monitor");
                    monitorTask = null;
                }
            }
        }

        public void Dispose()
        {
            log.Info("Disposing of Kubernetes Pod Monitor");
            Stop();
            cancellationTokenSource.Dispose();
        }
    }
}
