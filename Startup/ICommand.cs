using System;
using System.IO;

namespace Octopus.Shared.Startup
{
    public interface ICommand
    {
        void WriteHelp(TextWriter writer);
        void Start(string[] commandLineArguments, ICommandRuntime commandRuntime);
        void Stop();
    }
}
