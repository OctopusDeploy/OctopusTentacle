using System;

namespace Octopus.Tentacle.Contracts
{
    public static class ScriptExitCodes
    {
        public const int RunningExitCode = 0;
        public const int FatalExitCode = -41;
        public const int PowershellInvocationErrorExitCode = -42;
        public const int CanceledExitCode = -43;
        public const int TimeoutExitCode = -44;
        public const int UnknownScriptExitCode = -45;
        public const int UnknownResultExitCode = -46;
    }
}