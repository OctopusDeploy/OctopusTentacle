using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Diagnostics;

namespace Octopus.Shared.Configuration.EnvironmentVariableMappings
{
    public abstract class MapEnvironmentVariablesToConfigItems : IMapEnvironmentVariablesToConfigItems
    {
        readonly ILog log;
        readonly Dictionary<string, string?> environmentVariableValues;
        bool valuesHaveBeenSet;

        // These are the settings that calling code can ask for a value for
        string[] sharedConfigurationSettingNames =
        {
            HomeConfiguration.OctopusHomeSettingName,
            HomeConfiguration.OctopusNodeCacheSettingName,
            ProxyConfiguration.ProxyUseDefaultSettingName,
            ProxyConfiguration.ProxyHostSettingName,
            ProxyConfiguration.ProxyPortSettingName,
            ProxyConfiguration.ProxyUsernameSettingName,
            ProxyConfiguration.ProxyPasswordSettingName
        };
        
        // There are no required settings/variables in Shared. The
        // following are the name of the environment variables that
        // align with the above settings. 
        string[] sharedOptionalEnvironmentVariableNames =
        {
            "OCTOPUS_HOME",
            "OCTOPUS_NODE_CACHE",
            "OCTOPUS_PROXY_USE_DEFAULT",
            "OCTOPUS_PROXY_HOST",
            "OCTOPUS_PROXY_PORT",
            "OCTOPUS_PROXY_USERNAME",
            "OCTOPUS_PROXY_PASSWORD"
        };

        protected MapEnvironmentVariablesToConfigItems(ILog log, string[] supportedConfigurationKeys, string[] requiredEnvironmentVariables, string[] optionalEnvironmentVariables)
        {
            this.log = log;
            SupportedConfigurationKeys = new HashSet<string>(sharedConfigurationSettingNames.Union(supportedConfigurationKeys).OrderBy(x => x));
            RequiredEnvironmentVariables = new HashSet<string>(requiredEnvironmentVariables.OrderBy(x => x));
            SupportedEnvironmentVariables = new HashSet<string>(requiredEnvironmentVariables.Union(sharedOptionalEnvironmentVariableNames.Union(optionalEnvironmentVariables)).OrderBy(x => x));
            environmentVariableValues = new Dictionary<string, string?>();
            
            // initialise the dictionary to contain a value for every supported variable, then we don't need ContainsKey all over the place
            foreach (var variable in SupportedEnvironmentVariables)
            {
                environmentVariableValues.Add(variable, null);
            }
        }
        
        public ConfigState ConfigState
        {
            get
            {
                if (!valuesHaveBeenSet || SupportedEnvironmentVariables.All(v => string.IsNullOrWhiteSpace(environmentVariableValues[v])))
                    return ConfigState.None;
                {
                    if (RequiredEnvironmentVariables.All(v => !string.IsNullOrWhiteSpace(environmentVariableValues[v])))
                        return ConfigState.Complete;
                    
                    // We should never get here, SetEnvironmentValues should fail if required values are missing
                    log.Warn($"Some environment variables were found, but the following required variables were missing '{string.Join(", ", RequiredEnvironmentVariables.Where(v => string.IsNullOrWhiteSpace(environmentVariableValues[v])))}'");
                    return ConfigState.Partial;
                }

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

            // handle the shared setting here and pass others to the derived mapper
            switch (configurationSettingName)
            {
                case HomeConfiguration.OctopusHomeSettingName:
                    return environmentVariableValues["OCTOPUS_HOME"];
                case HomeConfiguration.OctopusNodeCacheSettingName:
                    return environmentVariableValues["OCTOPUS_NODE_CACHE"];
                case ProxyConfiguration.ProxyUseDefaultSettingName:
                    return environmentVariableValues["OCTOPUS_PROXY_USE_DEFAULT"];
                case ProxyConfiguration.ProxyHostSettingName:
                    return environmentVariableValues["OCTOPUS_PROXY_HOST"];
                case ProxyConfiguration.ProxyPortSettingName:
                    return environmentVariableValues["OCTOPUS_PROXY_PORT"];
                case ProxyConfiguration.ProxyUsernameSettingName:
                    return environmentVariableValues["OCTOPUS_PROXY_USERNAME"];
                case ProxyConfiguration.ProxyPasswordSettingName:
                    return environmentVariableValues["OCTOPUS_PROXY_PASSWORD"];
            }
            return MapConfigurationValue(configurationSettingName);
        }
        
        protected abstract string? MapConfigurationValue(string configurationSettingName);
    }
}