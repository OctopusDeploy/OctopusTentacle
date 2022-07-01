using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octopus.Shared.Configuration
{
    public abstract class JsonHierarchicalKeyValueStore : HierarchicalDictionaryKeyValueStore
    {
        protected readonly JsonSerializerSettings JsonSerializerSettings;

        protected JsonHierarchicalKeyValueStore(bool autoSaveOnSet, JsonSerializerSettings settings, bool isWriteOnly = false) : base(settings, autoSaveOnSet, isWriteOnly)
        {
            JsonSerializerSettings = settings;
        }

        protected override void SaveSettings(IDictionary<string, object?> settingsToSave)
        {
            var data = new ObjectHierarchy();
            foreach (var kvp in settingsToSave)
            {
                var keyHierarchyItems = kvp.Key.Split('.');

                var node = data;
                for (var i = 0; i < keyHierarchyItems.Length; i++)
                {
                    var keyHierarchyItem = keyHierarchyItems[i];

                    if (node != null && node.ContainsKey(keyHierarchyItem))
                    {
                        node = node[keyHierarchyItem] as ObjectHierarchy;
                    }
                    else
                    {
                        if (node != null && i == keyHierarchyItems.Length - 1)
                        {
                            node.Add(keyHierarchyItem, kvp.Value);
                        }
                        else
                        {
                            var newNode = new ObjectHierarchy();
                            node?.Add(keyHierarchyItem, newNode);
                            node = newNode;
                        }
                    }
                }
            }

            var serializedData = JsonConvert.SerializeObject(data, Formatting.Indented, JsonSerializerSettings);
            WriteSerializedData(serializedData);
        }

        protected abstract void WriteSerializedData(string serializedData);
    }

    public class ObjectHierarchy : SortedDictionary<string, object?>
    {
    }
}