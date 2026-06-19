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
        int? count;

        public SetPollingConnectionCountCommand(
            Lazy<IWritableTentacleConfiguration> configuration,
            IApplicationInstanceSelector instanceSelector,
            ISystemLog log,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(instanceSelector, log, logFileOnlyLogger)
        {
            this.configuration = configuration;
            this.log = log;

            Options.Add("count=", "The number of polling connections this Tentacle should open to each Octopus Server it polls. Only applies to polling Tentacles.", s => count = int.Parse(s));
        }

        protected override void Start()
        {
            base.Start();

            if (count is null)
                throw new ControlledFailureException("Please specify the number of polling connections, e.g. --count=5");

            if (count < 1)
                throw new ControlledFailureException("The polling connection count must be greater than 0.");

            configuration.Value.SetPollingConnectionCount(count.Value);
            log.Info($"Polling connection count set to: {count.Value}");
            VoteForRestart();
        }
    }
}
