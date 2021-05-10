using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    class EnvFileConfigurationStrategy : IApplicationConfigurationStrategy
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IEnvFileLocator envFileLocator;
        readonly IMapEnvironmentValuesToConfigItems mapper;
        bool loaded;
        bool foundValues;

        public EnvFileConfigurationStrategy(IOctopusFileSystem fileSystem, IEnvFileLocator envFileLocator, IMapEnvironmentValuesToConfigItems mapper)
        {
            this.fileSystem = fileSystem;
            this.envFileLocator = envFileLocator;
            this.mapper = mapper;
        }

        public int Priority => 2;

        public IAggregatableKeyValueStore? LoadContributedConfiguration()
        {
            EnsureLoaded();
            if (!foundValues)
                return null;
            
            return new InMemoryKeyValueStore(mapper);
        }

        void EnsureLoaded()
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