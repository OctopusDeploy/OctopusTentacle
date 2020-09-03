using System.Collections.Generic;

namespace Octopus.Shared.Configuration.EnvironmentVariableMappings
{
    public interface IMapEnvironmentVariablesToConfigItems
    {
        ConfigState ConfigState { get; }
        
        HashSet<string> SupportedEnvironmentVariables { get; }
        
        void SetEnvironmentValues(Dictionary<string, string?> variableNamesToValues);
        
        string? GetConfigurationValue(string configurationSettingName);
    }
}