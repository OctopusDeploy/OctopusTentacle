using System;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Internals.Options;

namespace Octopus.Tentacle.Startup
{
    public abstract class AbstractStandardCommand : AbstractCommand
    {
        readonly IApplicationInstanceSelector instanceSelector;

        bool voteForRestart;

        protected AbstractStandardCommand(IApplicationInstanceSelector instanceSelector, ISystemLog systemLog, ILogFileOnlyLogger logFileOnlyLogger) : base(logFileOnlyLogger)
        {
            this.instanceSelector = instanceSelector;
            SystemLog = systemLog;

            // The instance is actually parsed from the command-line as early as possible when the program starts to make sure logs end up in the most appropriate folder for the instance
            // See OctopusProgram.TryLoadInstanceName()
            // Adding the common "instance=" option here so every derived command adds this to their help message
            AddInstanceOption(Options);

            // These kinds of commands depend on being able to load the correct instance
            // Try and load it here just in case the implementing class forgets to call base.Start()
            // NOTE: Don't throw any exception in the constructor, otherwise we can't show help
            instanceSelector.CanLoadCurrentInstance();
        }

        protected ISystemLog SystemLog { get; }

        protected void VoteForRestart() => voteForRestart = true;

        protected override void Start()
        {
            // These kinds of commands depend on being able to load the correct instance
            // We need to assert the current instance can be loaded otherwise the rest of the command won't work as expected
            // NOTE: This method should throw a ControlledFailureException with the most appropriate message inside it
            var unused = instanceSelector.Current.InstanceName;
        }

        protected override void Completed()
        {
            base.Completed();

            if (voteForRestart)
            {
                var namedApplication = instanceSelector.Current.InstanceName != null ? instanceSelector.ApplicationName.ToString() : "service";
                SystemLog.Warn($"These changes require a restart of the {namedApplication}.");
            }
        }

        public static OptionSet AddInstanceOption(OptionSet options, Action<string>? instanceAction = null, Action<string>? configFileAction = null)
        {
            return options.Add("instance=", "Name of the instance to use", instanceAction ?? (v => { }))
                .Add("config=", "Configuration file to use", configFileAction ?? (v => { }));
        }
    }
}