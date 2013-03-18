using System;
using System.Collections.Generic;

namespace Octopus.Shared.Integration.Scripting
{
    [Serializable]
    public class ScriptExecutionResult
    {
        readonly int exitCode;
        readonly IDictionary<string, string> outputVariables;

        public ScriptExecutionResult(int exitCode, IDictionary<string, string> outputVariables = null)
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