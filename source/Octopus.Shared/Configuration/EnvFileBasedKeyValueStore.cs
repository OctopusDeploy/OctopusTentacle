using System;
using System.Linq;
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
        }

        public void Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
        }

        public void Remove(string name)
        {
        }

        public void Save()
        {
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
            var lines = content.Split(Environment.NewLine.ToCharArray());
            
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#")))
            {
                var splitIndex = line.IndexOf('=');
                if (splitIndex < 0)
                    throw new ArgumentException($"The line '{line}' is not formatted correctly");

                var key = line.Substring(0, splitIndex).Trim();
                var value = line.Substring(splitIndex + 1).Trim();
                
                if (mapper.SupportedEnvironmentVariables.Contains(key))
                    mapper.SetEnvironmentValue(key, value);
            }
        }
    }
}