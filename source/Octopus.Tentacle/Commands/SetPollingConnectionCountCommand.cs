using System;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class SetPollingConnectionCountCommand : AbstractStandardCommand
    {
        readonly Lazy<IWritableTentacleConfiguration> configuration;
        readonly ISystemLog log;
        string? countValue;

        public SetPollingConnectionCountCommand(
            Lazy<IWritableTentacleConfiguration> configuration,
            IApplicationInstanceSelector instanceSelector,
            ISystemLog log,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(instanceSelector, log, logFileOnlyLogger)
        {
            this.configuration = configuration;
            this.log = log;

            Options.Add(PollingConnectionCountOption.Prototype, PollingConnectionCountOption.Description, v => countValue = v);
        }

        protected override void Start()
        {
            base.Start();

            if (countValue is null)
                throw new ControlledFailureException($"Please specify the number of polling connections, e.g. --{PollingConnectionCountOption.Name}=5");

            var count = PollingConnectionCountOption.Parse(countValue);

            configuration.Value.SetPollingConnectionCount(count);
            log.Info($"Polling connection count set to: {count}");
            VoteForRestart();
        }
    }
}
