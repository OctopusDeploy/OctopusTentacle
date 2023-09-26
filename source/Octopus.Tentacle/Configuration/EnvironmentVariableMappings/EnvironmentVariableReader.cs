using System;

namespace Octopus.Tentacle.Configuration.EnvironmentVariableMappings
{
    public class EnvironmentVariableReader : IEnvironmentVariableReader
    {
        public string? Get(string variableName) => Environment.GetEnvironmentVariable(variableName);

        public TimeSpan GetOrDefault(string variableName, TimeSpan defaultValue)
        {
            var environmentVariableValue = Get(variableName);
            if (TimeSpan.TryParse(environmentVariableValue, out var timeSpan)) return timeSpan;

            return defaultValue;
        }
    }
}