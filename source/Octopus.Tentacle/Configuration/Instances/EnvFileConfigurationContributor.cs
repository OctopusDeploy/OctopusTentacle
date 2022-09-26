using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Tentacle.Configuration.EnvironmentVariableMappings;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Instances
{
    internal class EnvFileConfigurationContributor : IApplicationConfigurationContributor
    {
        private readonly IOctopusFileSystem fileSystem;
        private readonly IEnvFileLocator envFileLocator;
        private readonly IMapEnvironmentValuesToConfigItems mapper;
        private bool loaded;
        private bool foundValues;

        public EnvFileConfigurationContributor(IOctopusFileSystem fileSystem, IEnvFileLocator envFileLocator, IMapEnvironmentValuesToConfigItems mapper)
        {
            this.fileSystem = fileSystem;
            this.envFileLocator = envFileLocator;
            this.mapper = mapper;
        }

        public int Priority => 2;

        public IAggregatableKeyValueStore? LoadContributedConfiguration()
        {
            if (!ApplicationConfigurationContributionFlag.CanContributeSettings) return null;

            EnsureLoaded();
            if (!foundValues)
                return null;

            return new InMemoryKeyValueStore(mapper);
        }

        private void EnsureLoaded()
        {
            if (!loaded)
            {
                var results = LoadFromEnvFile(envFileLocator, fileSystem, mapper);
                if (results != null && results.Values.Any(x => x != null))
                {
                    mapper.SetEnvironmentValues(results);
                    foundValues = true;
                }
            }

            loaded = true;
        }

        internal static Dictionary<string, string?>? LoadFromEnvFile(IEnvFileLocator envFileLocator, IOctopusFileSystem fileSystem, IMapEnvironmentValuesToConfigItems mapper)
        {
            var envFile = envFileLocator.LocateEnvFile();
            if (envFile == null)
                return null;

            var content = fileSystem.ReadAllText(envFile);
            var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var results = new Dictionary<string, string?>();
            var lineNumber = 0;

            foreach (var line in lines)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var splitIndex = line.IndexOf('=');
                if (splitIndex < 0)
                    throw new ArgumentException($"Line {lineNumber} is not formatted correctly");

                var key = line.Substring(0, splitIndex).Trim();
                var value = line.Substring(splitIndex + 1).Trim();

                if (mapper.SupportedEnvironmentVariables.Any(v => v.Name == key))
                    results.Add(key, value);
            }

            return results;
        }
    }
}