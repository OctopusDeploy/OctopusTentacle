using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Configuration;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    public class EnvFileConfigurationStrategy : IApplicationConfigurationStrategy
    {
        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly IOctopusFileSystem fileSystem;
        readonly IEnvFileLocator envFileLocator;
        readonly IMapEnvironmentVariablesToConfigItems mapper;
        bool loaded;

        public EnvFileConfigurationStrategy(StartUpInstanceRequest startUpInstanceRequest, IOctopusFileSystem fileSystem, IEnvFileLocator envFileLocator, IMapEnvironmentVariablesToConfigItems mapper)
        {
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.fileSystem = fileSystem;
            this.envFileLocator = envFileLocator;
            this.mapper = mapper;
        }

        public int Priority => 300;

        public bool AnyInstancesConfigured()
        {
            if (!(startUpInstanceRequest is StartUpDynamicInstanceRequest))
                return false;
            var envFile = envFileLocator.LocateEnvFile();
            if (envFile == null)
                return false;

            EnsureLoaded();
            return mapper.ConfigState == ConfigState.Complete;
        }

        public IKeyValueStore? LoadedConfiguration(ApplicationInstanceRecord applicationInstance)
        {
            if (!(startUpInstanceRequest is StartUpDynamicInstanceRequest))
                return null;
            EnsureLoaded();
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
                }

            }
            loaded = true;
        }

        internal static Dictionary<string, string?>? LoadFromEnvFile(IEnvFileLocator envFileLocator, IOctopusFileSystem fileSystem, IMapEnvironmentVariablesToConfigItems mapper)
        {
            var envFile = envFileLocator.LocateEnvFile();
            if (envFile == null)
                return null;

            var content = fileSystem.ReadAllText(envFile);
            var lines = content.Split(new [] {  Environment.NewLine }, StringSplitOptions.None);
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

                if (mapper.SupportedEnvironmentVariables.Contains(key))
                    results.Add(key, value);
            }

            return results;
        }
    }
}