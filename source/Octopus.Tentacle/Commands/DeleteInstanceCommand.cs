using System;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class DeleteInstanceCommand : AbstractStandardCommand
    {
        private readonly IApplicationInstanceSelector instanceSelector;
        private readonly IApplicationInstanceManager instanceManager;

        public DeleteInstanceCommand(IApplicationInstanceSelector instanceSelector, IApplicationInstanceManager instanceManager, ISystemLog log, ILogFileOnlyLogger logFileOnlyLogger)
            : base(instanceSelector, log, logFileOnlyLogger)
        {
            this.instanceSelector = instanceSelector;
            this.instanceManager = instanceManager;
        }

        protected override void Start()
        {
            if (instanceSelector.Current.InstanceName != null) instanceManager.DeleteInstance(instanceSelector.Current.InstanceName);
        }
    }
}