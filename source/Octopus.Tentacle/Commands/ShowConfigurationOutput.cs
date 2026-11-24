using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    // Collects settings with dotted names and renders them as nested JSON.
    public class ShowConfigurationOutput
    {
        readonly Dictionary<string, object?> settings = new();

        public void Add(string name, object? value)
        {
            settings[name] = value is string s && string.IsNullOrWhiteSpace(s) ? null : value;
        }

        public void AddRange(IEnumerable<KeyValuePair<string, object?>> settingsToAdd)
        {
            foreach (var setting in settingsToAdd)
            {
                Add(setting.Key, setting.Value);
            }
        }

        public string ToJson()
        {
            var root = new SortedDictionary<string, object?>();
            foreach (var setting in settings)
            {
                var path = setting.Key.Split('.');
                var node = root;
                for (var i = 0; i < path.Length; i++)
                {
                    if (node != null && node.ContainsKey(path[i]))
                    {
                        node = node[path[i]] as SortedDictionary<string, object?>;
                    }
                    else if (node != null && i == path.Length - 1)
                    {
                        node.Add(path[i], setting.Value);
                    }
                    else
                    {
                        var child = new SortedDictionary<string, object?>();
                        node?.Add(path[i], child);
                        node = child;
                    }
                }
            }

            return JsonConvert.SerializeObject(root, Formatting.Indented, JsonSerialization.GetDefaultSerializerSettings());
        }
    }
}
