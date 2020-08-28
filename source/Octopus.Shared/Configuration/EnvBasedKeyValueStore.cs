using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Octopus.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class EnvBasedKeyValueStore : IKeyValueStore
    {
        readonly IOctopusFileSystem fileSystem;
        Dictionary<string, object?>? values;

        public EnvBasedKeyValueStore(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }
        
        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            var loadedValues = EnsureLoaded();
            return loadedValues.ContainsKey(name) ? loadedValues[name] as string : null;
        }

        public TData Get<TData>(string name, TData defaultValue = default(TData), ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            var loadedValues = EnsureLoaded();
            if (!loadedValues.ContainsKey(name)) 
                return defaultValue;

            var data = loadedValues[name];
            return (TData) data!;
        }

        public void Set(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            EnsureLoaded()[name] = value;
        }

        public void Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            EnsureLoaded()[name] = value;
        }

        public void Remove(string name)
        {
            EnsureLoaded().Remove(name);
        }

        public void Save()
        {
        }

        Dictionary<string, object?> EnsureLoaded()
        {
            if (values == null)
                values = LoadFromEnvFile();
            return values;
        }

        Dictionary<string, object?> LoadFromEnvFile()
        {
            var envFile = LocateEnvFile();
            if (envFile == null)
                throw new InvalidOperationException("Could not locate .env file");

            var content = fileSystem.ReadAllText(envFile);
            var lines = content.Split(Environment.NewLine.ToCharArray());
            var results = new Dictionary<string, object?>();
            
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#")))
            {
                var kvp = line.Split('=');
                if (kvp.Length < 2)
                    throw new ArgumentException($"The line '{line}' is not formatted correctly");
                results.Add(kvp[0].Trim(), kvp[1].Trim());
            }

            return results;
        }

        string? LocateEnvFile()
        {
            var directoryToCheck = Path.GetDirectoryName(typeof(EnvBasedKeyValueStore).Assembly.Location);

            var envPathToCheck = Path.Combine(directoryToCheck, ".env");
            var envFileExists = fileSystem.FileExists(envPathToCheck);
            var rootDirectoryReached = false;

            while (!envFileExists && !rootDirectoryReached)
            {
                var lastPathSeparator = directoryToCheck.LastIndexOf(Path.DirectorySeparatorChar);
                directoryToCheck = directoryToCheck.Substring(0, lastPathSeparator);

                if (lastPathSeparator >= 0 && lastPathSeparator <= 2)
                    rootDirectoryReached = true;

                if (rootDirectoryReached)
                    directoryToCheck += Path.DirectorySeparatorChar; // for root path when need to tack the separator back on
                
                envPathToCheck = Path.Combine(directoryToCheck, ".env");
                
                envFileExists = fileSystem.FileExists(envPathToCheck);
            }

            return envFileExists ? envPathToCheck : null;
        }
    }
}