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

        protected readonly ISystemLog Log;
        readonly TimeSpan terminationGracePeriod;

        Task? backgroundTask;

        protected BackgroundTask(ISystemLog log, TimeSpan terminationGracePeriod)
        {
            name = GetType().Name;
            this.Log = log;
            this.terminationGracePeriod = terminationGracePeriod;
        }

        protected abstract Task RunTask(CancellationToken cancellationToken);

        public void Start()
        {
            lock (@lock)
            {
                if (backgroundTask is not null)
                {
                    Log.Error($"{name}.Start(): Already running.");
                    return;
                }

                Log.Info($"{name}.Start(): Starting");
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
                    Log.Info($"{name}.Stop(): Stopping");
                    cancellationTokenSource.Cancel();

                    backgroundTask.Wait(terminationGracePeriod);
                }
                catch (Exception e)
                {
                    Log.Error(e, $"{name}.Stop(): Could not stop");
                }
                finally
                {
                    Log.Info($"{name}.Stop(): Stopped");
                    backgroundTask = null;
                }
            }
        }

        public void Dispose()
        {
            Log.Info($"{name}.Dispose(): Disposing");
            Stop();
            cancellationTokenSource.Dispose();
        }
    }
}