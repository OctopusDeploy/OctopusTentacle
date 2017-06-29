using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Shared.Internals.Options;

namespace Octopus.Shared.Startup
{
    public abstract class AbstractCommand : ICommand
    {
        readonly List<ICommandOptions> optionSets = new List<ICommandOptions>();

        public virtual bool SuppressConsoleLogging => false;

        public OptionSet Options { get; } = new OptionSet();

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

        protected abstract void Start();
        protected virtual void Completed() { }

        protected virtual void Stop()
        {
        }

        void ICommand.WriteHelp(TextWriter writer)
        {
            Options.WriteOptionDescriptions(writer);
        }

        public virtual void Start(string[] commandLineArguments, ICommandRuntime commandRuntime, OptionSet commonOptions)
        {
            Runtime = commandRuntime;

            var unrecognized = Options.Parse(commandLineArguments);
            UnrecognizedArguments(unrecognized);

            foreach (var opset in optionSets)
                opset.Validate();

            LogFileOnlyLogger.Info($"==== {GetType().Name} ====");
            Start();
            Completed();
        }

        void ICommand.Stop()
        {
            Stop();
        }

        protected void AssertNotNullOrWhitespace(string value, string name, string errorMessage = null)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ControlledFailureException(errorMessage ?? $"--{name} must be supplied");
        }

        protected void AssertFileExists(string path, string errorMessage = null)
        {
            if (!File.Exists(path))
                throw new ControlledFailureException(errorMessage ?? $"File '{path}' cannot be found");
        }

        protected void AssertFileDoesNotExist(string path, string errorMessage = null)
        {
            if (File.Exists(path))
            {
                throw new ControlledFailureException(errorMessage ?? $"A file already exists at '{path}'");
            }
        }

        protected void AssertDirectoryExists(string path, string errorMessage = null)
        {
            if (!Directory.Exists(path))
                throw new ControlledFailureException(errorMessage ?? $"Directory '{path}' cannot be found");
        }

        static readonly Lazy<List<string>> SpecialLocations = new Lazy<List<string>>(() =>
        {
            var result = new List<string>();
            foreach (var specialLocation in Enum.GetValues(typeof(Environment.SpecialFolder)))
            {
                var location = Environment.GetFolderPath((Environment.SpecialFolder)specialLocation, Environment.SpecialFolderOption.None);
                result.Add(location);
            }
            result.Add("C:");
            result.Add("C:\\");
            return result;
        });

        protected void AssertNotSpecialLocation(string path)
        {
            foreach (var specialLocation in SpecialLocations.Value)
            {
                if (string.Equals(path, specialLocation, StringComparison.OrdinalIgnoreCase))
                    throw new ControlledFailureException($"Directory '{path}' is not a good place, pick a safe subdirectory");
            }
        }
    }
}