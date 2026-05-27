using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Background;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Diagnostics
{
    public interface ILivenessHeartbeatTask : IBackgroundTask
    {
    }

    class LivenessHeartbeatTask : BackgroundTask, ILivenessHeartbeatTask
    {
        public const string HeartbeatFileName = "livez.heartbeat";
        static readonly TimeSpan DefaultTickInterval = TimeSpan.FromSeconds(10);

        readonly IHomeConfiguration homeConfiguration;
        readonly TimeSpan tickInterval;

        public LivenessHeartbeatTask(IHomeConfiguration homeConfiguration, ISystemLog log)
            : this(homeConfiguration, log, DefaultTickInterval)
        {
        }

        internal LivenessHeartbeatTask(IHomeConfiguration homeConfiguration, ISystemLog log, TimeSpan tickInterval)
            : base(log, TimeSpan.FromSeconds(2))
        {
            this.homeConfiguration = homeConfiguration;
            this.tickInterval = tickInterval;
        }

        protected override async Task RunTask(CancellationToken cancellationToken)
        {
            var heartbeatPath = ResolveHeartbeatPath();
            if (heartbeatPath is null)
            {
                Log.Warn("Liveness heartbeat task could not resolve a home directory; heartbeat file will not be written.");
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                TouchHeartbeat(heartbeatPath);

                try
                {
                    await Task.Delay(tickInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        string? ResolveHeartbeatPath()
        {
            var home = homeConfiguration.HomeDirectory;
            return string.IsNullOrWhiteSpace(home) ? null : Path.Combine(home, HeartbeatFileName);
        }

        void TouchHeartbeat(string path)
        {
            try
            {
                using (File.Create(path))
                {
                }
            }
            catch (IOException e)
            {
                Log.Warn(e, $"Failed to update liveness heartbeat file at {path}.");
            }
            catch (UnauthorizedAccessException e)
            {
                Log.Warn(e, $"Failed to update liveness heartbeat file at {path}.");
            }
        }
    }
}
