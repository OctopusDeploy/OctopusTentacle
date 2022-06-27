using System;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Startup;

namespace Octopus.Tentacle.Commands
{
    public class DeleteInstanceCommand : AbstractStandardCommand
    {
        readonly IApplicationInstanceSelector instanceSelector;
        readonly IApplicationInstanceManager instanceManager;

        public DeleteInstanceCommand(IApplicationInstanceSelector instanceSelector, IApplicationInstanceManager instanceManager, ISystemLog log, ILogFileOnlyLogger logFileOnlyLogger)
            : base(instanceSelector, log, logFileOnlyLogger)
        {
            this.instanceSelector = instanceSelector;
            this.instanceManager = instanceManager;
        }

        protected override void Start()
        {
            instanceManager.DeleteInstance(instanceSelector.Current.InstanceName);
        }
    }
}