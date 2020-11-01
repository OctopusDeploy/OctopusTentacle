using System;

namespace Octopus.Shared.Configuration.EnvironmentVariableMappings
{
    public class EnvironmentVariableReader : IEnvironmentVariableReader
    {
        public string? Get(string variableName)
        {
            return Environment.GetEnvironmentVariable(variableName);
        }
    }
}