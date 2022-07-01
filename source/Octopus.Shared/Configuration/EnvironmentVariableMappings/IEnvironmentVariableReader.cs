using System;

namespace Octopus.Shared.Configuration.EnvironmentVariableMappings
{
    public interface IEnvironmentVariableReader
    {
        string? Get(string variableName);
    }
}