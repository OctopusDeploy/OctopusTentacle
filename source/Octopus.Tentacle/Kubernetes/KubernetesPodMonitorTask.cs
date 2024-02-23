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

        static readonly object LockObj = new();

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
                monitorTask = Task.Run(() => podMonitor.StartAsync(cancellationTokenSource.Token));
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

                    monitorTask.Wait();
                }
                catch (Exception e)
                {
                    log.Error(e, "Could not stop Kubernetes Pod Monitor cleaner");
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