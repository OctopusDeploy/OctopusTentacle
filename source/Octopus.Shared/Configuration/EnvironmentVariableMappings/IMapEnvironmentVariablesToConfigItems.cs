namespace Octopus.Shared.Configuration.EnvironmentVariableMappings
{
    public interface IMapEnvironmentVariablesToConfigItems
    {
        string[] SupportedConfigurationKeys { get; }
        
        string[] SupportedEnvironmentVariables { get; }

        void SetEnvironmentVariableValue(string key, string value);

        string GetConfigurationValue(string key);
    }
}