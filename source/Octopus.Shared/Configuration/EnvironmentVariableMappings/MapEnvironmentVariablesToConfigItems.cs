using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Configuration.EnvironmentVariableMappings
{
    public abstract class MapEnvironmentVariablesToConfigItems : IMapEnvironmentVariablesToConfigItems
    {
        readonly Dictionary<string, string?> environmentVariableValues;
        bool valuesHaveBeenSet;

        string[] sharedConfigurationKeys =
        {
            HomeConfiguration.OctopusHome,
            HomeConfiguration.OctopusNodeCache,
            ProxyConfiguration.OctopusProxyUseDefault,
            ProxyConfiguration.OctopusProxyHost,
            ProxyConfiguration.OctopusProxyPort,
            ProxyConfiguration.OctopusProxyUsername,
            ProxyConfiguration.OctopusProxyPassword
        };
        
        string[] sharedOptionalVariables =
        {
            "OCTOPUS_HOME",
            "OCTOPUS_NODE_CACHE",
            "OCTOPUS_PROXY_USE_DEFAULT",
            "OCTOPUS_PROXY_HOST",
            "OCTOPUS_PROXY_PORT",
            "OCTOPUS_PROXY_USERNAME",
            "OCTOPUS_PROXY_PASSWORD"
        };

        protected MapEnvironmentVariablesToConfigItems(string[] supportedConfigurationKeys, string[] requiredEnvironmentVariables, string[] optionalEnvironmentVariables)
        {
            SupportedConfigurationKeys = new HashSet<string>(sharedConfigurationKeys.Union(supportedConfigurationKeys).OrderBy(x => x));
            RequiredEnvironmentVariables = new HashSet<string>(requiredEnvironmentVariables.OrderBy(x => x));
            SupportedEnvironmentVariables = new HashSet<string>(requiredEnvironmentVariables.Union(sharedOptionalVariables.Union(optionalEnvironmentVariables)).OrderBy(x => x));
            environmentVariableValues = new Dictionary<string, string?>();
            
            // initialise the dictionary to contain a value for every supported variable, then we don't need ContainsKey all over the place
            foreach (var variable in SupportedEnvironmentVariables)
            {
                environmentVariableValues.Add(variable, null);
            }
        }

        HashSet<string> SupportedConfigurationKeys { get; }
        
        HashSet<string> RequiredEnvironmentVariables { get; }
        public HashSet<string> SupportedEnvironmentVariables { get; }

        protected IReadOnlyDictionary<string, string?> EnvironmentValues => environmentVariableValues;

        public void SetEnvironmentValues(Dictionary<string, string?> variableNamesToValues)
        {
            var unsupportedVariables = variableNamesToValues.Keys.OrderBy(x => x).Where(x => !SupportedEnvironmentVariables.Contains(x)).ToArray();
            if (unsupportedVariables.Any())
            {
                var pluralString = unsupportedVariables.Length == 1 ? " was" : "s were";
                throw new ArgumentException($"Unsupported environment variable{pluralString} provided. '{string.Join(", ", unsupportedVariables)}'");
            }

            var missingRequiredVariables = RequiredEnvironmentVariables.Where(x => !variableNamesToValues.ContainsKey(x) || string.IsNullOrWhiteSpace(variableNamesToValues[x])).ToArray();
            if (missingRequiredVariables.Any())
            {
                var pluralString = missingRequiredVariables.Length == 1 ? " was" : "s were";
                throw new ArgumentException($"Required environment variable{pluralString} not provided. '{string.Join(", ", missingRequiredVariables)}'");
            }

            foreach (var nameToValue in variableNamesToValues)
            {
                environmentVariableValues[nameToValue.Key] = nameToValue.Value;
            }

            valuesHaveBeenSet = true;
        }

        public string? GetConfigurationValue(string configurationSettingName)
        {
            if (!SupportedConfigurationKeys.Contains(configurationSettingName))
                throw new ArgumentException($"Given configuration setting name is not supported. '{configurationSettingName}'");
            if (!valuesHaveBeenSet)
                throw new InvalidOperationException("No variable values have been specified.");
            
            return MapConfigurationValue(configurationSettingName);
        }
        
        protected abstract string? MapConfigurationValue(string configurationSettingName);
    }
}