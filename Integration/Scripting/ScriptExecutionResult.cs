using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Integration.Scripting
{
    [Serializable]
    public class ScriptExecutionResult
    {
        readonly int exitCode;
        readonly bool stdErrorWritten;
        readonly IDictionary<string, string> outputVariables;
        readonly ICollection<CreatedArtifact> createdArtifacts;

        public ScriptExecutionResult(
            int exitCode, 
            bool stdErrorWritten, 
            IDictionary<string, string> outputVariables = null,
            IEnumerable<CreatedArtifact> createdArtifacts = null)
        {
            this.exitCode = exitCode;
            this.stdErrorWritten = stdErrorWritten;
            this.outputVariables = outputVariables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.createdArtifacts = (createdArtifacts ?? new List<CreatedArtifact>()).ToList();
        }

        public bool StdErrorWritten { get { return stdErrorWritten; } }

        public int ExitCode
        {
            get { return exitCode; }
        }

        public IDictionary<string, string> OutputVariables
        {
            get { return outputVariables; }
        }

        public ICollection<CreatedArtifact> CreatedArtifacts
        {
            get { return createdArtifacts; }
        }
    }
}