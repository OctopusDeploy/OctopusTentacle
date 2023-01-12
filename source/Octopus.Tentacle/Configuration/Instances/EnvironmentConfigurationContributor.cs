using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Tentacle.Configuration.EnvironmentVariableMappings;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Configuration.Instances
{
    class EnvironmentConfigurationContributor : IApplicationConfigurationContributor
    {
        readonly ILogFileOnlyLogger log;
        readonly IMapEnvironmentValuesToConfigItems mapper;
        readonly IEnvironmentVariableReader reader;
        bool loaded;
        bool foundValues;

        public EnvironmentConfigurationContributor(ILogFileOnlyLogger log,
            IMapEnvironmentValuesToConfigItems mapper,
            IEnvironmentVariableReader reader)
        {
            this.log = log;
            this.mapper = mapper;
            this.reader = reader;
        }

        public int Priority => 1;
        public IAggregatableKeyValueStore? LoadContributedConfiguration()
        {
            if (!ApplicationConfigurationContributionFlag.CanContributeSettings)
            {
                return null;
            }
            
            EnsureLoaded();
            if (foundValues)
            {
                return new InMemoryKeyValueStore(mapper);
            }
            return null;
        }

        void EnsureLoaded()
        {
            if (!loaded)
            {
                var results = LoadFromEnvironment(log, reader, mapper);
                if (results.Values.Any(x => x != null))
                {
                    mapper.SetEnvironmentValues(results);
                    foundValues = true;
                }
            }

            loaded = true;
        }

        internal static Dictionary<string, string?> LoadFromEnvironment(ILogFileOnlyLogger log, IEnvironmentVariableReader reader, IMapEnvironmentValuesToConfigItems mapper)
        {
            var results = new Dictionary<string, string?>();
            foreach (var variable in mapper.SupportedEnvironmentVariables)
            {
                var value = reader.Get(variable.Name);

                // if we actually get a value for a sensitive variable from an environment variable, let's recommend not doing that in Prod
                if (variable is SensitiveEnvironmentVariable sensitive && !string.IsNullOrWhiteSpace(value))
                    log.Warn($"We noticed that you're picking up {sensitive.WarningDescription} from an environment variable. This is OK for development/test environments but we do not recommend it in a production environment as the key can be comprised quite easily in a number of scenarios.");

                results.Add(variable.Name, value);
            }

            return results;
        }
    }
}