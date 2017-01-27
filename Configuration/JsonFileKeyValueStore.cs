using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class JsonFileKeyValueStore : JsonKeyValueStore
    {
        readonly string configurationFile;
        readonly IOctopusFileSystem fileSystem;

        public JsonFileKeyValueStore(string configurationFile, IOctopusFileSystem fileSystem, bool useHierarchicalOutput, bool autoSaveOnSet, bool isWriteOnly = false) : base(useHierarchicalOutput, autoSaveOnSet, isWriteOnly)
        {
            this.configurationFile = configurationFile;
            this.fileSystem = fileSystem;
        }

        protected override void LoadSettings(IDictionary<string, string> settingsToFill)
        {
            if (!fileSystem.FileExists(configurationFile))
            {
                return;
            }

            Dictionary<string, string> deserializedData;
            using (var reader = new StreamReader(fileSystem.OpenFile(configurationFile, FileMode.Open)))
            {
                var serializedData = reader.ReadToEnd();
                deserializedData = JsonConvert.DeserializeObject<Dictionary<string, string>>(serializedData);
            }
            foreach (var kvp in deserializedData)
            {
                settingsToFill.Add(kvp.Key, kvp.Value);
            }
        }

        protected override void WriteSerializedData(string serializedData)
        {
            var parentDirectory = Path.GetDirectoryName(configurationFile);
            fileSystem.EnsureDirectoryExists(parentDirectory);

            using (var writer = new StreamWriter(fileSystem.OpenFile(configurationFile, FileMode.Create)))
            {
                writer.Write(serializedData);
            }
        }
    }
}