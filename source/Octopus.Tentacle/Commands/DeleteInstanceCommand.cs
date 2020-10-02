using System;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Startup;

namespace Octopus.Tentacle.Commands
{
    public class DeleteInstanceCommand : AbstractStandardCommand
    {
        readonly IApplicationInstanceSelector instanceSelector;

        public DeleteInstanceCommand(IApplicationInstanceSelector instanceSelector) : base(instanceSelector)
        {
            this.instanceSelector = instanceSelector;
        }

        protected override void Start()
        {
            instanceSelector.DeleteInstance();
        }
    }
}