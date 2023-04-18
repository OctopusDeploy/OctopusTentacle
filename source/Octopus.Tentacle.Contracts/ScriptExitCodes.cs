using System;

namespace Octopus.Tentacle.Contracts
{
    public static class ScriptExitCodes
    {
        public const int FatalExitCode = -41;
        public const int PowershellInvocationErrorExitCode = -42;
        public const int CanceledExitCode = -43;
        public const int TimeoutExitCode = -44;
    }
}