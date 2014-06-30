using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Shared.Internals.Options;

namespace Octopus.Shared.Startup
{
    public abstract class AbstractCommand : ICommand
    {
        readonly OptionSet options = new OptionSet();

        protected OptionSet Options { get { return options; }}

        readonly List<ICommandOptions> optionSets = new List<ICommandOptions>();

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

        protected ICommandRuntime Runtime { get; private set; }

        protected abstract void Start();

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

            Start();
        }

        void ICommand.Stop()
        {
            Stop();
        }
    }
}