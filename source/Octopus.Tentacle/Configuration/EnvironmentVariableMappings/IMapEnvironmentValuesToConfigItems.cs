using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Configuration.EnvironmentVariableMappings
{
    public interface IMapEnvironmentValuesToConfigItems
    {
        HashSet<EnvironmentVariable> SupportedEnvironmentVariables { get; }

        void SetEnvironmentValues(Dictionary<string, string?> variableNamesToValues);

        string? GetConfigurationValue(string configurationSettingName);
    }
}