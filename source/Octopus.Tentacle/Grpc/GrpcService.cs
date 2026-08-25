using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Grpc
{
    public abstract class GrpcService : IGrpcService, IDisposable
    {
        readonly string name;
        readonly object @lock = new();
        
        protected  ISystemLog Log { get; }
        
        readonly CancellationTokenSource cancellationTokenSource = new ();

        Task? grpcServiceExecution;
        
        protected GrpcService(ISystemLog log)
        {
            name = GetType().Name;
            Log = log;
        }

        protected abstract Task Execute(CancellationToken cancellationToken);
        
        public void Start()
        {
            lock (@lock)
            {
                if (grpcServiceExecution is not null)
                {
                    Log.Error($"{name}.Start(): Already running.");
                    return;
                }

                Log.Info($"{name}.Start(): Starting");
                grpcServiceExecution = Task.Run(() => Execute(cancellationTokenSource.Token));
            }
        }

        public void Stop()
        {
            lock (@lock)
            {
                if (grpcServiceExecution is null) return;

                try
                {
                    Log.Info($"{name}.Stop(): Stopping");
                    cancellationTokenSource.Cancel();

                    grpcServiceExecution.Wait(TimeSpan.FromSeconds(30));
                }
                catch (Exception e)
                {
                    Log.Error(e, $"{name}.Stop(): Could not stop");
                }
                finally
                {
                    Log.Info($"{name}.Stop(): Stopped");
                    grpcServiceExecution = null;
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