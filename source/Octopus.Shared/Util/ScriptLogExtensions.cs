using System;

namespace Octopus.Shared.Util
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
    }
}