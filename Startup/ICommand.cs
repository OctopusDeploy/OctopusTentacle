using System;
using System.IO;
using Octopus.Shared.Internals.Options;

namespace Octopus.Shared.Startup
{
    public interface ICommand
    {
        void WriteHelp(TextWriter writer);

        // Common options are provided so that the Help command can inspect them
        void Start(string[] commandLineArguments, ICommandRuntime commandRuntime, OptionSet commonOptions);
        void Stop();
    }
}
