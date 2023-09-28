using System;

namespace Octopus.Tentacle
{
    public sealed class ScriptExitCodeException : Exception
    {
        public int ExitCode { get; }

        public ScriptExitCodeException(int exitCode)
        {
            ExitCode = exitCode;
        }
    }
}