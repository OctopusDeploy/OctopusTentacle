using Octopus.Shared.Configuration;

namespace Octopus.Shared.Startup
{
    public abstract class AbstractStandardCommand : AbstractCommand
    {
        readonly IApplicationInstanceSelector instanceSelector;

        protected AbstractStandardCommand(IApplicationInstanceSelector instanceSelector)
        {
            this.instanceSelector = instanceSelector;
            // The instance is actually parsed from the command-line as early as possible when the program starts to make sure logs end up in the most appropriate folder for the instance
            // See OctopusProgram.TryLoadInstanceName()
            // Adding the common "instance=" option here so every derived command add this to their help message
            Options.Add("instance=", "Name of the instance to use", v => { });

            // These kinds of commands depend on being able to load the correct instance
            // Try and load it here just in case the implementing class forgets to call base.Start()
            // NOTE: Don't throw any exception in the constructor, otherwise we can't show help
            instanceSelector.TryLoadCurrentInstance(out var unused);
        }

        protected override void Start()
        {
            // These kinds of commands depend on being able to load the correct instance
            // We need to assert the current instance can be loaded otherwise the rest of the command won't work as expected
            var unused = instanceSelector.Current;
        }
    }
}