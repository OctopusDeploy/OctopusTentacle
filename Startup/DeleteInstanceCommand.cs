using System;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Startup
{
    public class DeleteInstanceCommand : AbstractCommand
    {
        readonly IApplicationInstanceSelector instanceSelector;
        string instanceName;
        
        public DeleteInstanceCommand(IApplicationInstanceSelector instanceSelector)
        {
            this.instanceSelector = instanceSelector;
            Options.Add("instance=", "Name of the instance to delete", v => instanceName = v);
        }

        protected override void Start()
        {
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                instanceSelector.DeleteDefaultInstance();
            }
            else
            {
                instanceSelector.DeleteInstance(instanceName);
            }
        }
    }
}