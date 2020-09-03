using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Configuration;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;

namespace Octopus.Shared.Configuration
{
    public class EnvironmentBasedKeyValueStore : IKeyValueStore
    {
        readonly IMapEnvironmentVariablesToConfigItems mapper;
        readonly IEnvironmentVariableReader reader;
        bool loaded;

        public EnvironmentBasedKeyValueStore(IMapEnvironmentVariablesToConfigItems mapper, IEnvironmentVariableReader reader)
        {
            this.mapper = mapper;
            this.reader = reader;
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
            throw new InvalidOperationException($"Persisting the {name} configuration value back to an environment variable is not supported");
        }

        public void Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            throw new InvalidOperationException($"Persisting the {name} configuration value back to an environment variable is not supported");
        }

        public void Remove(string name)
        {
            throw new InvalidOperationException($"Persisting the {name} configuration value back to an environment variable is not supported");
        }

        public void Save()
        {
            throw new InvalidOperationException($"Persisting configuration values back to environment variables is not supported");
        }
        
        void EnsureLoaded()
        {
            if (!loaded)
                LoadFromEnvironment();
            loaded = true;
        }

        void LoadFromEnvironment()
        {
            var results = new Dictionary<string, string?>();
            foreach (var variableName in mapper.SupportedEnvironmentVariables)
            {
                results.Add(variableName, reader.Get(variableName));
            }
            mapper.SetEnvironmentValues(results);
        }
    }
}