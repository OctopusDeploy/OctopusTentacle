using Octopus.Shared.Configuration;

namespace Octopus.Shared.Startup
{
    public abstract class AbstractStandardCommand : AbstractCommand
    {
        protected AbstractStandardCommand(IApplicationInstanceSelector instanceSelector)
        {
            // The instance is actually parsed from the command-line as early as possible when the program starts to make sure logs end up in the most appropriate folder for the instance
            // Adding the common "instance=" option here so every derived command add this to their help message
            Options.Add("instance=", "Name of the instance to use", v => { });

            // These kinds of commands depend on being able to load the correct instance
            // Ensure this is the case by loading the current instance
            var currentInstance = instanceSelector.Current;
        }
    }
}