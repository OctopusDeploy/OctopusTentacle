using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Shared.Internals.Options;

namespace Octopus.Shared.Startup
{
    public abstract class AbstractCommand : ICommand
    {
        readonly OptionSet options = new OptionSet();
        readonly List<ICommandOptions> optionSets = new List<ICommandOptions>();

        protected OptionSet Options
        {
            get { return options; }
        }

        protected ICommandRuntime Runtime { get; private set; }

        protected TOptionSet AddOptionSet<TOptionSet>(TOptionSet commandOptions)
            where TOptionSet : class, ICommandOptions
        {
            if (commandOptions == null) throw new ArgumentNullException("commandOptions");
            optionSets.Add(commandOptions);
            return commandOptions;
        }

        protected virtual void UnrecognizedArguments(IList<string> arguments)
        {
            if (arguments.Count > 0)
            {
                throw new ArgumentException("Unrecognized command line arguments: " + string.Join(" ", arguments));
            }
        }

        protected virtual void Initialize() { }
        protected abstract void Start();
        protected virtual void Completed() { }

        protected virtual void Stop()
        {
        }

        void ICommand.WriteHelp(TextWriter writer)
        {
            Options.WriteOptionDescriptions(writer);
        }

        void ICommand.Start(string[] commandLineArguments, ICommandRuntime commandRuntime, OptionSet commonOptions)
        {
            Runtime = commandRuntime;

            var unrecognized = options.Parse(commandLineArguments);
            UnrecognizedArguments(unrecognized);

            foreach (var opset in optionSets)
                opset.Validate();

            Initialize();
            Start();
            Completed();
        }

        void ICommand.Stop()
        {
            Stop();
        }
    }
}