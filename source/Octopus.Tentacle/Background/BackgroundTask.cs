using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Background
{
    public interface IBackgroundTask
    {
        void Start();
        void Stop();
    }

    public abstract class BackgroundTask : IBackgroundTask, IDisposable
    {
        readonly string name;
        readonly CancellationTokenSource cancellationTokenSource = new ();
        readonly object @lock = new();

        protected readonly ISystemLog log;
        readonly TimeSpan terminationGracePeriod;

        Task? backgroundTask;

        protected BackgroundTask(ISystemLog log, TimeSpan terminationGracePeriod)
        {
            name = GetType().Name;
            this.log = log;
            this.terminationGracePeriod = terminationGracePeriod;
        }

        protected abstract Task RunTask(CancellationToken cancellationToken);

        public void Start()
        {
            lock (@lock)
            {
                if (backgroundTask is not null)
                {
                    log.Error($"{name}.Start(): Already running.");
                    return;
                }

                log.Info($"{name}.Start(): Starting");
                backgroundTask = Task.Run(() => RunTask(cancellationTokenSource.Token));
            }
        }

        public void Stop()
        {
            lock (@lock)
            {
                if (backgroundTask is null) return;

                try
                {
                    log.Info($"{name}.Stop(): Stopping");
                    cancellationTokenSource.Cancel();

                    backgroundTask.Wait(terminationGracePeriod);
                }
                catch (Exception e)
                {
                    log.Error(e, $"{name}.Stop(): Could not stop");
                }
                finally
                {
                    log.Info($"{name}.Stop(): Stopped");
                    backgroundTask = null;
                }
            }
        }

        public void Dispose()
        {
            log.Info($"{name}.Dispose(): Disposing");
            Stop();
            cancellationTokenSource.Dispose();
        }
    }
}