using System;
using System.Collections.Generic;
using Octopus.Shared.ServiceMessages;

namespace Octopus.Shared.Integration.Scripting
{
    /// <summary>
    /// Uses a service message parser to collect deployment-specific information from
    /// a script's output.
    /// </summary>
    public class ScriptExecutionOutputFilter
    {
        readonly ServiceMessageParser parser;
        readonly IDictionary<string, string> outputVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); 
        readonly ICollection<string> createdArtifacts = new List<string>(); 

        public ScriptExecutionOutputFilter(ScriptOutput outputStream)
        {
            if (outputStream == null) throw new ArgumentNullException("outputStream");

            parser = new ServiceMessageParser(
                outputStream.OnWritten,
                message =>
                {
                    if (message.Name == ScriptServiceMessageNames.SetVariable.Name)
                    {
                        outputVariables[message.GetValue(ScriptServiceMessageNames.SetVariable.NameAttribute)] = 
                            message.GetValue(ScriptServiceMessageNames.SetVariable.ValueAttribute);
                    }
                    else if (message.Name == ScriptServiceMessageNames.CreateArtifact.Name)
                    {
                        createdArtifacts.Add(message.GetValue(ScriptServiceMessageNames.CreateArtifact.PathAttribute));
                    }
                });
        }

        public IDictionary<string, string> OutputVariables
        {
            get { return outputVariables; }
        }

        public ICollection<string> CreatedArtifacts
        {
            get { return createdArtifacts; }
        }

        public void WriteLine(string line)
        {
            parser.Append(line + Environment.NewLine);
        }
    }
}