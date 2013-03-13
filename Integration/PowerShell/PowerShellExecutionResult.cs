using System;
using System.Collections.Generic;

namespace Octopus.Shared.Integration.PowerShell
{
    [Serializable]
    public class PowerShellExecutionResult
    {
        readonly int exitCode;
        readonly IDictionary<string, string> outputVariables;

        public PowerShellExecutionResult(int exitCode, IDictionary<string, string> outputVariables = null)
        {
            this.exitCode = exitCode;
            this.outputVariables = outputVariables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public int ExitCode
        {
            get { return exitCode; }
        }

        public IDictionary<string, string> OutputVariables
        {
            get { return outputVariables; }
        }
    }
}