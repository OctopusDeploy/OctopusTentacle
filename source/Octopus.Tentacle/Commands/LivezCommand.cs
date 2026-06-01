using System;
using System.IO;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Time;

namespace Octopus.Tentacle.Commands
{
    public class LivezCommand : AbstractStandardCommand
    {
        const int DefaultMaxAgeSeconds = 60;

        readonly Lazy<IHomeConfiguration> homeConfiguration;
        readonly IClock clock;

        int maxAgeSeconds = DefaultMaxAgeSeconds;

        public LivezCommand(
            Lazy<IHomeConfiguration> homeConfiguration,
            IClock clock,
            IApplicationInstanceSelector instanceSelector,
            ISystemLog log,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(instanceSelector, log, logFileOnlyLogger)
        {
            this.homeConfiguration = homeConfiguration;
            this.clock = clock;

            Options.Add(
                "max-age=",
                $"Maximum allowed age of the liveness heartbeat in seconds (default {DefaultMaxAgeSeconds}).",
                v => maxAgeSeconds = int.Parse(v));
        }

        protected override void Start()
        {
            base.Start();

            if (maxAgeSeconds <= 0)
                throw new ControlledFailureException("--max-age must be a positive number of seconds.");

            var home = homeConfiguration.Value.HomeDirectory;
            if (string.IsNullOrWhiteSpace(home))
                throw new ControlledFailureException("Could not determine the Tentacle home directory for the selected instance.");

            var heartbeatPath = Path.Combine(home!, LivenessHeartbeatTask.HeartbeatFileName);

            if (!File.Exists(heartbeatPath))
                throw new ControlledFailureException($"Liveness heartbeat file not found at {heartbeatPath}; the Tentacle agent may not be running.");

            var lastWriteUtc = File.GetLastWriteTimeUtc(heartbeatPath);
            var age = clock.GetUtcTime().UtcDateTime - lastWriteUtc;

            if (age > TimeSpan.FromSeconds(maxAgeSeconds))
                throw new ControlledFailureException($"Liveness heartbeat at {heartbeatPath} is stale: last written {(int)age.TotalSeconds}s ago (threshold {maxAgeSeconds}s).");
        }
    }
}
