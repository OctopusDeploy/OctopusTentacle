using System;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Util
{
    public static class ScriptLogExtensions
    {
        public static void WriteWait(this Action<string> log, string message)
        {
            log("##octopus[stdout-wait]");
            log(message);
            log("##octopus[stdout-default]");
        }

        public static void WriteVerbose(this Action<string> log, string message)
        {
            log("##octopus[stdout-verbose]");
            log(message);
            log("##octopus[stdout-default]");
        }

        public static void WriteVerbose(this IScriptLogWriter writer, string message)
        {
            writer.WriteOutput(ProcessOutputSource.StdOut, "##octopus[stdout-verbose]");
            writer.WriteOutput(ProcessOutputSource.StdOut, message);
            writer.WriteOutput(ProcessOutputSource.StdOut, "##octopus[stdout-default]");
        }
    }
}