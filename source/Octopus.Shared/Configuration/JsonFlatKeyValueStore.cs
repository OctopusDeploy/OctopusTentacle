using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octopus.Shared.Configuration
{
    public abstract class JsonFlatKeyValueStore : FlatDictionaryKeyValueStore
    {
        protected JsonFlatKeyValueStore(bool autoSaveOnSet, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
        }

        protected override void SaveSettings(IDictionary<string, object> settingsToSave)
        {
            WriteSerializedData(JsonConvert.SerializeObject(new SortedDictionary<string, object>(settingsToSave), Formatting.Indented));
        }

        protected abstract void WriteSerializedData(string serializedData);
    }
}
