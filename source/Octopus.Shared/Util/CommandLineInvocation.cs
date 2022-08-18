using System;

namespace Octopus.Shared.Util
{
    public class CommandLineInvocation
    {
        public CommandLineInvocation(string executable, string arguments, string? systemArguments = null)
        {
            Executable = executable;
            Arguments = arguments;
            SystemArguments = systemArguments;
        }

        public string Executable { get; }

        public string Arguments { get; }

        // Arguments only used when we are invoking this directly from within the tools - not used when 
        // exporting the script for use later.
        public string? SystemArguments { get; }

        public bool IgnoreFailedExitCode { get; set; }

        public override string ToString()
            => "\"" + Executable + "\" " + Arguments;
    }
}