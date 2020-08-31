using System.Collections.Generic;

namespace Octopus.Shared.Configuration.EnvironmentVariableMappings
{
    public interface IMapEnvironmentVariablesToConfigItems
    {
        HashSet<string> SupportedEnvironmentVariables { get; }
        
        void SetEnvironmentValue(string variableName, string? value);
        string? GetConfigurationValue(string configurationSettingName);
    }
}