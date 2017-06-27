using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Internals.Options;

namespace Octopus.Shared.Startup
{
    public abstract class AbstractCommand : ICommand
    {
        readonly List<ICommandOptions> optionSets = new List<ICommandOptions>();
        bool showLogo = true;
        static readonly ILogWithContext Log = Diagnostics.Log.Octopus();

        protected AbstractCommand()
        {
            Options.Add("nologo", "Don't print title or version information", v => showLogo = false);
        }

        protected OptionSet Options { get; } = new OptionSet();

        protected ICommandRuntime Runtime { get; private set; }

        protected TOptionSet AddOptionSet<TOptionSet>(TOptionSet commandOptions)
            where TOptionSet : class, ICommandOptions
        {
            if (commandOptions == null) throw new ArgumentNullException(nameof(commandOptions));
            optionSets.Add(commandOptions);
            return commandOptions;
        }

        protected virtual void UnrecognizedArguments(IList<string> arguments)
        {
            if (arguments.Count > 0)
            {
                throw new ControlledFailureException("Unrecognized command line arguments: " + string.Join(" ", arguments));
            }
        }

        protected virtual void Initialize(string displayName, string version, string informationalVersion, string[] environmentInformation, string instanceName)
        {
            if (showLogo)
            {
                var instanceNameToLog = string.IsNullOrWhiteSpace(instanceName) ? "Default" : instanceName;
                Log.Info($"{displayName} version {version} ({informationalVersion}) instance {instanceNameToLog}");
                Log.Info($"Environment Information:{Environment.NewLine}" +
                    $"  {string.Join($"{Environment.NewLine}  ", environmentInformation)}");
            }
            Log.Info($"==== {GetType().Name} ====");
        }

        protected abstract void Start();
        protected virtual void Completed() { }

        protected virtual void Stop()
        {
        }

        void ICommand.WriteHelp(TextWriter writer)
        {
            Options.WriteOptionDescriptions(writer);
        }

        void ICommand.Start(string[] commandLineArguments, ICommandRuntime commandRuntime, OptionSet commonOptions, string displayName, string version, string informationalVersion, string[] environmentInformation, string instanceName)
        {
            Runtime = commandRuntime;

            var unrecognized = Options.Parse(commandLineArguments);
            UnrecognizedArguments(unrecognized);

            foreach (var opset in optionSets)
                opset.Validate();

            Initialize(displayName, version, informationalVersion, environmentInformation, instanceName);
            Start();
            Completed();
        }

        void ICommand.Stop()
        {
            Stop();
        }
    }
}