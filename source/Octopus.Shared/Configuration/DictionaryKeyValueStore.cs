using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Configuration
{
    public abstract class DictionaryKeyValueStore : AbstractKeyValueStore
    {
        private readonly Lazy<IDictionary<string, object>> settings;

        protected DictionaryKeyValueStore(bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet)
        {
            settings = isWriteOnly ? new Lazy<IDictionary<string, object>>(() => new Dictionary<string, object>()) : new Lazy<IDictionary<string, object>>(Load);
        }

        protected void Write(string key, object value)
        {
            settings.Value[key] = value;
        }

        protected object Read(string key)
        {
            return settings.Value.TryGetValue(key, out var result) ? result : null;
        }

        protected override void Delete(string key)
        {
            settings.Value.Remove(key);
        }

        public sealed override void Save()
        {
            SaveSettings(settings.Value);
        }

        private IDictionary<string, object> Load()
        {
            var dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            LoadSettings(dictionary);
            return dictionary;
        }

        protected virtual void LoadSettings(IDictionary<string, object> settingsToFill)
        {
        }

        protected virtual void SaveSettings(IDictionary<string, object> settingsToSave)
        {
        }

        public override string ToString()
        {
            return string.Concat(settings.Value.Select(x => $"{x.Key}: {x.Value}\n"));
        }
    }
}
