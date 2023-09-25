using System;

namespace Octopus.Tentacle
{
    public class ScriptExitCodeException : Exception
    {
        public int ExitCode { get; }

        public ScriptExitCodeException(int exitCode)
        {
            ExitCode = exitCode;
        }
    }
}