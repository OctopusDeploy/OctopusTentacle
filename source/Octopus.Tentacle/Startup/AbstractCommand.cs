using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Octopus.Tentacle.Internals.Options;

namespace Octopus.Tentacle.Startup
{
    public abstract class AbstractCommand : ICommand
    {
        private readonly List<ICommandOptions> optionSets = new();
        private ICommandRuntime? runtime;

        protected AbstractCommand(ILogFileOnlyLogger logFileOnlyLogger)
        {
            LogFileOnlyLogger = logFileOnlyLogger;
        }

        protected ILogFileOnlyLogger LogFileOnlyLogger { get; }

        public virtual bool SuppressConsoleLogging => false;
        public virtual bool CanRunAsService => false;

        public OptionSet Options { get; } = new();

        protected ICommandRuntime Runtime
        {
            get
            {
                if (runtime == null)
                    throw new InvalidOperationException("Start hasn't set the Runtime correctly!");
                return runtime;
            }
        }

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
                throw new ControlledFailureException("Unrecognized command line arguments: " + string.Join(" ", arguments));
        }

        protected abstract void Start();

        protected virtual void Completed()
        {
        }

        protected virtual void Stop()
        {
        }

        void ICommand.WriteHelp(TextWriter writer)
        {
            Options.WriteOptionDescriptions(writer);
        }

        public virtual void Start(string[] commandLineArguments, ICommandRuntime commandRuntime, OptionSet commonOptions)
        {
            runtime = commandRuntime;

            var unrecognized = Options.Parse(commandLineArguments);
            UnrecognizedArguments(unrecognized);

            foreach (var opset in optionSets)
                opset.Validate();

            EnsureSensitiveParametersAreNotLoggedToLogFileOnlyLogger();

            LogFileOnlyLogger.Info($"==== {GetType().Name} ====");
            LogFileOnlyLogger.Info($"CommandLine: {string.Join(" ", Environment.GetCommandLineArgs())}");

            Start();
            Completed();
        }

        private void EnsureSensitiveParametersAreNotLoggedToLogFileOnlyLogger()
        {
            foreach (var sensitiveOption in Options.Where(x => x.Sensitive))
            foreach (var name in sensitiveOption.GetNames())
            {
                var option = Options.GetOptionForName(name);
                if (option == null)
                    throw new ArgumentException($"Unable to locate options for command named {name}");
                //Ideally, we'd ensure that no logging anywhere would log these values, but its way harder than it sounds.
                //The LogContext is immutable, and designed so that is a tree structure - usually, you only care about
                //sensitive values for the scope of a deployment, not after that. Changing it to be not immutable
                //is very bad from a perf perspective, as the AhoCorasick masking algorithm is pretty perf intensive.
                //Also, due to timing issues of when loggers are created, it gets very difficult to ensure that the logger
                //will have the sensitive values set. Its all rather complicated, and its preventing a theoretical problem
                //whereas this LogFileOnlyLogger is definitely logging sensitive values, so we need to mask there
                LogFileOnlyLogger.AddSensitiveValues(option.Values.Where(x => x != null).ToArray()!);
            }
        }

        void ICommand.Stop()
        {
            Stop();
        }
    }
}