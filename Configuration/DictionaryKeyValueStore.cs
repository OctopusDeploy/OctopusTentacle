using System;
using System.Collections.Generic;

namespace Octopus.Shared.Configuration
{
    public class DictionaryKeyValueStore : AbstractKeyValueStore
    {
        readonly Lazy<IDictionary<string, string>> settings;

        public DictionaryKeyValueStore()
        {
            settings = new Lazy<IDictionary<string, string>>(Load);
        }

        protected override sealed void Write(string key, string value)
        {
            settings.Value[key] = value;
        }

        protected override sealed string Read(string key)
        {
            string result;
            return settings.Value.TryGetValue(key, out result) ? result : null;
        }

        public override sealed void Save()
        {
            SaveSettings(settings.Value);
        }

        IDictionary<string, string> Load()
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            LoadSettings(dictionary);
            return dictionary;
        }

        protected virtual void LoadSettings(IDictionary<string, string> settingsToFill)
        {
        }

        protected virtual void SaveSettings(IDictionary<string, string> settingsToSave)
        {
        }
    }
}