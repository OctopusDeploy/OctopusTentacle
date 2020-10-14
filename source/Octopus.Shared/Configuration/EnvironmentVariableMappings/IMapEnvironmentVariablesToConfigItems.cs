using System.Collections.Generic;

namespace Octopus.Shared.Configuration.EnvironmentVariableMappings
{
    public interface IMapEnvironmentVariablesToConfigItems
    {
        HashSet<string> SupportedEnvironmentVariables { get; }

        void SetEnvironmentValues(Dictionary<string, string?> variableNamesToValues);

        string? GetConfigurationValue(string configurationSettingName);
    }
}