using System;
using Octopus.Shared.Contracts;
using Octopus.Shared.Scripts;

namespace Octopus.Shared.Util
{
    public static class ScriptLogExtensions
    {
        public static void WriteVerbose(this Action<string> log, string message)
        {
            log("##octopus[stdout-verbose]");
            log(message);
            log("##octopus[stdout-default]");
        }
    }
}