using System;
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Internals.Options;

namespace Octopus.Shared.Startup
{
    public abstract class AbstractStandardCommand : AbstractCommand
    {
        protected static readonly ILogWithContext Log = Diagnostics.Log.System();

        readonly IApplicationInstanceSelector instanceSelector;

        bool voteForRestart;
        protected void VoteForRestart() => voteForRestart = true;

        protected AbstractStandardCommand(IApplicationInstanceSelector instanceSelector)
        {
            this.instanceSelector = instanceSelector;

            // The instance is actually parsed from the command-line as early as possible when the program starts to make sure logs end up in the most appropriate folder for the instance
            // See OctopusProgram.TryLoadInstanceName()
            // Adding the common "instance=" option here so every derived command adds this to their help message
            AddInstanceOption(Options);

            // These kinds of commands depend on being able to load the correct instance
            // Try and load it here just in case the implementing class forgets to call base.Start()
            // NOTE: Don't throw any exception in the constructor, otherwise we can't show help
            instanceSelector.TryGetCurrentInstance(out var unused);
        }

        protected override void Start()
        {
            // These kinds of commands depend on being able to load the correct instance
            // We need to assert the current instance can be loaded otherwise the rest of the command won't work as expected
            // NOTE: This method should throw a ControlledFailureException with the most appropriate message inside it
            var unused = instanceSelector.GetCurrentInstance();
        }

        protected override void Completed()
        {
            base.Completed();

            if (voteForRestart)
            {
                var applicationName = instanceSelector.TryGetCurrentInstance(out var instance) ? instance.ApplicationDescription : "service";
                Log.Warn($"These changes require a restart of the {applicationName}.");
            }
        }

        public static OptionSet AddInstanceOption(OptionSet options, Action<string> configurationHomeAction = null, Action<string> instanceAction = null)
        {
            return options
                .Add("instance=", "Name of the instance to use", instanceAction ?? (v => {}))
                .Add("machineConfigurationHomeDirectory=", "Home directory for the machine's configuration files", configurationHomeAction ?? (v => {}));
        }
    }
}