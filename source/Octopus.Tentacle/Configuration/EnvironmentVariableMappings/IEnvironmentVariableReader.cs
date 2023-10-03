using System;

namespace Octopus.Tentacle.Configuration.EnvironmentVariableMappings
{
    public interface IEnvironmentVariableReader
    {
        string? Get(string variableName);
        TimeSpan GetOrDefault(string variableName, TimeSpan defaultValue);
    }
}