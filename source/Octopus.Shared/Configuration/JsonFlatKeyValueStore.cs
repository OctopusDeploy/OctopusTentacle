using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octopus.Shared.Configuration
{
    public abstract class JsonFlatKeyValueStore : FlatDictionaryKeyValueStore
    {
        protected JsonFlatKeyValueStore(bool autoSaveOnSet, JsonSerializerSettings jsonSerializerSettings, bool isWriteOnly = false) : base(jsonSerializerSettings, autoSaveOnSet, isWriteOnly)
        {
        }

        protected override void SaveSettings(IDictionary<string, object?> settingsToSave)
        {
            WriteSerializedData(JsonConvert.SerializeObject(new SortedDictionary<string, object?>(settingsToSave), Formatting.Indented, JsonSerializerSettings));
        }

        protected abstract void WriteSerializedData(string serializedData);
    }
}