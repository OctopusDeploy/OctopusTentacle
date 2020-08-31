using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration.EnvironmentVariableMappings
{
    public abstract class MapEnvironmentVariablesToConfigItems : IMapEnvironmentVariablesToConfigItems
    {
        readonly Dictionary<string, string> environmentVariableValues;

        public MapEnvironmentVariablesToConfigItems(string[] supportedConfigurationKeys, string[] supportedEnvironmentVariables)
        {
            SupportedConfigurationKeys = supportedConfigurationKeys;
            SupportedEnvironmentVariables = supportedEnvironmentVariables;
            environmentVariableValues = new Dictionary<string, string>();
        }

        public string[] SupportedConfigurationKeys { get; }
        public string[] SupportedEnvironmentVariables { get; }

        protected IReadOnlyDictionary<string, string> EnvironmentVariableValues => environmentVariableValues;

        public void SetEnvironmentVariableValue(string key, string value)
        {
            environmentVariableValues[key] = value;
        }

        public abstract string GetConfigurationValue(string key);
    }
}