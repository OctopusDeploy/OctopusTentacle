using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Configuration;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class EnvFileBasedKeyValueStore : IKeyValueStore
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IEnvFileLocator envFileLocator;
        readonly IMapEnvironmentVariablesToConfigItems mapper;
        bool loaded;

        public EnvFileBasedKeyValueStore(IOctopusFileSystem fileSystem, IEnvFileLocator envFileLocator, IMapEnvironmentVariablesToConfigItems mapper)
        {
            this.fileSystem = fileSystem;
            this.envFileLocator = envFileLocator;
            this.mapper = mapper;
        }
        
        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            EnsureLoaded();
            return mapper.GetConfigurationValue(name);
        }

        public TData Get<TData>(string name, TData defaultValue = default(TData), ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            EnsureLoaded();
            
            var data = mapper.GetConfigurationValue(name);

            if (data == null)
                return default(TData)!;
            if (typeof(TData) == typeof(byte[]))
                return (TData)(object)Encoding.ASCII.GetBytes(data);
            return (TData)(object)data;
        }

        public void Set(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            throw new InvalidOperationException($"Persisting the {name} configuration value through the EnvFileBaseKeyValueStore is not supported");
        }

        public void Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            throw new InvalidOperationException($"Persisting the {name} configuration value through the EnvFileBaseKeyValueStore is not supported");
        }

        public void Remove(string name)
        {
            throw new InvalidOperationException($"Persisting the {name} configuration value through the EnvFileBaseKeyValueStore is not supported");
        }

        public void Save()
        {
            throw new InvalidOperationException($"Persisting configuration values through the EnvFileBaseKeyValueStore is not supported");
        }

        void EnsureLoaded()
        {
            if (!loaded)
                LoadFromEnvFile();
            loaded = true;
        }

        void LoadFromEnvFile()
        {
            var envFile = envFileLocator.LocateEnvFile();
            if (envFile == null)
                throw new InvalidOperationException("Could not locate .env file");

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
            
            mapper.SetEnvironmentValues(results);
        }
    }
}