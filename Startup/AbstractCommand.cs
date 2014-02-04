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

            Start();
        }

        void ICommand.Stop()
        {
            Stop();
        }
    }
}