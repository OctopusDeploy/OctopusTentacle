using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration
{
    public class JsonHierarchicalFileKeyValueStore : JsonHierarchicalKeyValueStore
    {
        private readonly string configurationFile;
        private readonly IOctopusFileSystem fileSystem;

        public JsonHierarchicalFileKeyValueStore(string configurationFile, IOctopusFileSystem fileSystem, bool autoSaveOnSet, bool isWriteOnly = false) : base(autoSaveOnSet, JsonSerialization.GetDefaultSerializerSettings(), isWriteOnly)
        {
            this.configurationFile = configurationFile;
            this.fileSystem = fileSystem;
        }

        protected override void LoadSettings(IDictionary<string, object?> settingsToFill)
        {
            if (!fileSystem.FileExists(configurationFile))
                return;

            Dictionary<string, string>? deserializedData;
            using (var reader = new StreamReader(fileSystem.OpenFile(configurationFile, FileMode.Open)))
            {
                var serializedData = reader.ReadToEnd();
                deserializedData = JsonConvert.DeserializeObject<Dictionary<string, string>>(serializedData, JsonSerializerSettings);
            }

            if (deserializedData?.Count > 0)
                foreach (var kvp in deserializedData)
                    settingsToFill.Add(kvp.Key, kvp.Value);
        }

        protected override void WriteSerializedData(string serializedData)
        {
            var parentDirectory = Path.GetDirectoryName(configurationFile) ?? throw new Exception("Configuration file location must include directory information");
            fileSystem.EnsureDirectoryExists(parentDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(configurationFile, 1024 * 1024);
            using (var writer = new StreamWriter(fileSystem.OpenFile(configurationFile, FileMode.Create)))
            {
                writer.Write(serializedData);
            }
        }
    }
}