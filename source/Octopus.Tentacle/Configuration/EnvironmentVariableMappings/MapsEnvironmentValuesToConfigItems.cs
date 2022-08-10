using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Configuration.EnvironmentVariableMappings
{
    public abstract class MapsEnvironmentValuesToConfigItems : IMapEnvironmentValuesToConfigItems
    {
        static readonly EnvironmentVariable Home = EnvironmentVariable.PlaintText("OCTOPUS_HOME");
        static readonly EnvironmentVariable NodeCache = EnvironmentVariable.PlaintText("OCTOPUS_NODE_CACHE");
        static readonly EnvironmentVariable UseDefaultProxy = EnvironmentVariable.PlaintText("OCTOPUS_USE_DEFAULT_PROXY");
        static readonly EnvironmentVariable ProxyHost = EnvironmentVariable.PlaintText("OCTOPUS_PROXY_HOST");
        static readonly EnvironmentVariable ProxyPort = EnvironmentVariable.PlaintText("OCTOPUS_PROXY_PORT");
        static readonly EnvironmentVariable ProxyUser = EnvironmentVariable.PlaintText("OCTOPUS_PROXY_USER");
        static readonly SensitiveEnvironmentVariable ProxyPassword = EnvironmentVariable.Sensitive("OCTOPUS_PROXY_PASSWORD", "proxy password");

        // There are no required settings/variables in Shared. The
        // following are the name of the environment variables that
        // align with the above settings.
        static readonly EnvironmentVariable[] SharedOptionalEnvironmentVariables =
        {
            Home,
            NodeCache,
            UseDefaultProxy,
            ProxyHost,
            ProxyPort,
            ProxyUser,
            ProxyPassword
        };

        readonly ILogFileOnlyLogger log;
        readonly Dictionary<string, string?> environmentVariableValues;

        // These are the settings that calling code can ask for a value for
        readonly string[] sharedConfigurationSettingNames =
        {
            HomeConfiguration.OctopusHomeSettingName,
            HomeConfiguration.OctopusNodeCacheSettingName,
            ProxyConfiguration.ProxyUseDefaultSettingName,
            ProxyConfiguration.ProxyHostSettingName,
            ProxyConfiguration.ProxyPortSettingName,
            ProxyConfiguration.ProxyUsernameSettingName,
            ProxyConfiguration.ProxyPasswordSettingName
        };

        bool valuesHaveBeenSet;

        protected MapsEnvironmentValuesToConfigItems(ILogFileOnlyLogger log,
            string[] supportedConfigurationKeys,
            EnvironmentVariable[] requiredEnvironmentVariables,
            EnvironmentVariable[] optionalEnvironmentVariables)
        {
            this.log = log;
            SupportedConfigurationKeys = new HashSet<string>(sharedConfigurationSettingNames.Union(supportedConfigurationKeys).OrderBy(x => x));
            RequiredEnvironmentVariables = new HashSet<EnvironmentVariable>(requiredEnvironmentVariables.OrderBy(x => x.Name));
            SupportedEnvironmentVariables = new HashSet<EnvironmentVariable>(requiredEnvironmentVariables.Union(SharedOptionalEnvironmentVariables.Union(optionalEnvironmentVariables)).OrderBy(x => x));
            environmentVariableValues = new Dictionary<string, string?>();

            // initialise the dictionary to contain a value for every supported variable, then we don't need ContainsKey all over the place
            foreach (var variable in SupportedEnvironmentVariables)
                environmentVariableValues.Add(variable.Name, null);
        }

        HashSet<string> SupportedConfigurationKeys { get; }

        HashSet<EnvironmentVariable> RequiredEnvironmentVariables { get; }
        public HashSet<EnvironmentVariable> SupportedEnvironmentVariables { get; }

        protected IReadOnlyDictionary<string, string?> EnvironmentValues => environmentVariableValues;

        public void SetEnvironmentValues(Dictionary<string, string?> variableNamesToValues)
        {
            var unsupportedVariables = variableNamesToValues.Keys.OrderBy(x => x).Where(x => SupportedEnvironmentVariables.All(v => v.Name != x)).ToArray();
            if (unsupportedVariables.Any())
            {
                var pluralString = unsupportedVariables.Length == 1 ? " was" : "s were";
                throw new ArgumentException($"Unsupported environment variable{pluralString} provided. '{string.Join(", ", unsupportedVariables)}'");
            }

            var missingRequiredVariableNames = RequiredEnvironmentVariables
                .Where(x => !variableNamesToValues.ContainsKey(x.Name) || string.IsNullOrWhiteSpace(variableNamesToValues[x.Name]))
                .Select(x => x.Name)
                .ToArray();
            if (missingRequiredVariableNames.Any())
            {
                var pluralString = missingRequiredVariableNames.Length == 1 ? " was" : "s were";
                throw new ArgumentException($"Required environment variable{pluralString} not provided. '{string.Join(", ", missingRequiredVariableNames)}'");
            }

            foreach (var nameToValue in variableNamesToValues)
                // Only try to set a value if a higher priority strategy hasn't already set it.
                if (string.IsNullOrWhiteSpace(environmentVariableValues[nameToValue.Key]) && !string.IsNullOrWhiteSpace(nameToValue.Value))
                    environmentVariableValues[nameToValue.Key] = nameToValue.Value;
                else if (!string.IsNullOrWhiteSpace(environmentVariableValues[nameToValue.Key]) && !string.IsNullOrWhiteSpace(nameToValue.Value))
                    log.Warn($"A value for '{nameToValue.Key}' has been provided more than once. This can happen, for example, if an environment variable is set but a higher priority provider like a secrets file has already provided the value.");

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
                    return environmentVariableValues[Home.Name];
                case HomeConfiguration.OctopusNodeCacheSettingName:
                    return environmentVariableValues[NodeCache.Name];
                case ProxyConfiguration.ProxyUseDefaultSettingName:
                    return environmentVariableValues[UseDefaultProxy.Name];
                case ProxyConfiguration.ProxyHostSettingName:
                    return environmentVariableValues[ProxyHost.Name];
                case ProxyConfiguration.ProxyPortSettingName:
                    return environmentVariableValues[ProxyPort.Name];
                case ProxyConfiguration.ProxyUsernameSettingName:
                    return environmentVariableValues[ProxyUser.Name];
                case ProxyConfiguration.ProxyPasswordSettingName:
                    return environmentVariableValues[ProxyPassword.Name];
            }

            return MapConfigurationValue(configurationSettingName);
        }

        protected abstract string? MapConfigurationValue(string configurationSettingName);
    }
}