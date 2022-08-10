using System;

namespace Octopus.Tentacle.Configuration.EnvironmentVariableMappings
{
    public interface IEnvironmentVariableReader
    {
        string? Get(string variableName);
    }
}