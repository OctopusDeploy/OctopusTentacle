using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration.EnvironmentVariableMappings
{
    public abstract class MapEnvironmentVariablesToConfigItems : IMapEnvironmentVariablesToConfigItems
    {
        readonly Dictionary<string, string?> environmentVariableValues;

        protected MapEnvironmentVariablesToConfigItems(string[] supportedConfigurationKeys, string[] supportedEnvironmentVariables)
        {
            SupportedConfigurationKeys = new HashSet<string>(supportedConfigurationKeys);
            SupportedEnvironmentVariables = new HashSet<string>(supportedEnvironmentVariables);
            environmentVariableValues = new Dictionary<string, string?>();
            
            // initialise the dictionary to contain a value for every supported variable, then we don't need ContainsKey all over the place
            foreach (var variable in supportedEnvironmentVariables)
            {
                environmentVariableValues.Add(variable, null);
            }
        }

        public HashSet<string> SupportedConfigurationKeys { get; }
        public HashSet<string> SupportedEnvironmentVariables { get; }

        protected IReadOnlyDictionary<string, string?> EnvironmentValues => environmentVariableValues;

        public void SetEnvironmentValue(string variableName, string? value)
        {
            if (!SupportedEnvironmentVariables.Contains(variableName))
                throw new ArgumentException("Given variable name is not support", nameof(variableName));
            environmentVariableValues[variableName] = value;
        }
        
        public abstract string? GetConfigurationValue(string configurationSettingName);
    }
}