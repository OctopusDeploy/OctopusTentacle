using System;

namespace Octopus.Tentacle.Configuration.EnvironmentVariableMappings
{
    public class EnvironmentVariableReader : IEnvironmentVariableReader
    {
        public string? Get(string variableName)
            => Environment.GetEnvironmentVariable(variableName);
    }
}