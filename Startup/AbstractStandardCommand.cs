using Octopus.Shared.Configuration;

namespace Octopus.Shared.Startup
{
    public abstract class AbstractStandardCommand : AbstractCommand
    {
        readonly IApplicationInstanceSelector instanceSelector;
        string instanceName;

        protected AbstractStandardCommand(IApplicationInstanceSelector instanceSelector)
        {
            this.instanceSelector = instanceSelector;
            Options.Add("instance=", "Name of the instance to use", v => instanceName = v);
        }

        protected override void Start()
        {
        }
    }
}