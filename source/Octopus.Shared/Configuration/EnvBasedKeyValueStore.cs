using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Configuration;
using Octopus.Diagnostics;
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
            if (data == null)
                return default(TData)!;
            if (typeof(TData) != typeof(string))
            {
                return JsonConvert.DeserializeObject<TData>((string)data, JsonSerialization.GetDefaultSerializerSettings());
            }
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
            var envFile = LocateEnvFile(fileSystem, null);
            if (envFile == null)
                throw new InvalidOperationException("Could not locate .env file");

            var content = fileSystem.ReadAllText(envFile);
            var lines = content.Split(Environment.NewLine.ToCharArray());
            var results = new Dictionary<string, object?>();
            
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#")))
            {
                var splitIndex = line.IndexOf('=');
                if (splitIndex < 0)
                    throw new ArgumentException($"The line '{line}' is not formatted correctly");
                var key = line.Substring(0, splitIndex).Trim();
                var value = line.Substring(splitIndex + 1).Trim();
                results.Add(key, value);
            }

            return results;
        }

        internal static string? LocateEnvFile(IOctopusFileSystem fileSystem, ILog? log)
        {
            var directoryToCheck = Path.GetDirectoryName(typeof(EnvBasedKeyValueStore).Assembly.Location);

            if (log != null)
            {
                log.InfoFormat("Search for .env file, starting from {0}", directoryToCheck);
            }

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

            if (log != null)
                if (envFileExists)
                    log.InfoFormat("Found .env file, {0}", envPathToCheck);
                else
                    log.Info("No .env file found");

            return envFileExists ? envPathToCheck : null;
        }
    }
}