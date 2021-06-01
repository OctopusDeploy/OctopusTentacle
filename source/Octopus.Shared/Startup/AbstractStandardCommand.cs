using System;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Internals.Options;

namespace Octopus.Shared.Startup
{
    public abstract class AbstractStandardCommand : AbstractCommand
    {
        protected static readonly ILogWithContext Log = Diagnostics.Log.System();

        readonly IApplicationInstanceSelector instanceSelector;

        bool voteForRestart;

        protected AbstractStandardCommand(IApplicationInstanceSelector instanceSelector, ILogFileOnlyLogger logFileOnlyLogger) : base(logFileOnlyLogger)
        {
            this.instanceSelector = instanceSelector;

            // The instance is actually parsed from the command-line as early as possible when the program starts to make sure logs end up in the most appropriate folder for the instance
            // See OctopusProgram.TryLoadInstanceName()
            // Adding the common "instance=" option here so every derived command adds this to their help message
            AddInstanceOption(Options);

            // These kinds of commands depend on being able to load the correct instance
            // Try and load it here just in case the implementing class forgets to call base.Start()
            // NOTE: Don't throw any exception in the constructor, otherwise we can't show help
            instanceSelector.CanLoadCurrentInstance();
        }

        protected void VoteForRestart() => voteForRestart = true;

        protected override void Start()
        {
            // These kinds of commands depend on being able to load the correct instance
            // We need to assert the current instance can be loaded otherwise the rest of the command won't work as expected
            // NOTE: This method should throw a ControlledFailureException with the most appropriate message inside it
            var unused = instanceSelector.GetCurrentName();
        }

        protected override void Completed()
        {
            base.Completed();

            if (voteForRestart)
            {
                var applicationName = instanceSelector.GetCurrentName() != null ? instanceSelector.ApplicationName.ToString() : "service";
                Log.Warn($"These changes require a restart of the {applicationName}.");
            }
        }

        public static OptionSet AddInstanceOption(OptionSet options, Action<string>? instanceAction = null, Action<string>? configFileAction = null)
        {
            return options.Add("instance=", "Name of the instance to use", instanceAction ?? (v => { }))
                .Add("config=", "Configuration file to use", configFileAction ?? (v => { }));
        }
    }
}