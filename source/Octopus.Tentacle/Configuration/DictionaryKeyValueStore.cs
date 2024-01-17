using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Tentacle.Configuration
{
    public abstract class DictionaryKeyValueStore : KeyValueStoreBase
    {
        Lazy<IDictionary<string, object?>> settings;

        protected DictionaryKeyValueStore(bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet)
        {
            settings = isWriteOnly ? new Lazy<IDictionary<string, object?>>(() => new Dictionary<string, object?>()) : new Lazy<IDictionary<string, object?>>(Load);
        }

        protected void Write(string key, object? value)
        {
            settings.Value[key] = value;
        }

        protected object? Read(string key)
            => settings.Value.TryGetValue(key, out var result) ? result : null;

        protected override void Delete(string key)
        {
            settings.Value.Remove(key);
        }

        public sealed override bool Save()
        {
            SaveSettings(settings.Value);

            // we're reloading on save because at this point the dictionary may contain non-string values (i.e. int/bool/etc)
            // after reload the dictionary will contain the formatted strings that are converted during the Get<T> call
            // Reads vastly outnumber writes so performance is not really a concern here.
            // Also, future plan is to move as much configuration to the database as possible.
            settings = new Lazy<IDictionary<string, object?>>(Load);
            return true;
        }

        IDictionary<string, object?> Load()
        {
            var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            LoadSettings(dictionary);
            return dictionary;
        }

        protected virtual void LoadSettings(IDictionary<string, object?> settingsToFill)
        {
        }

        protected virtual void SaveSettings(IDictionary<string, object?> settingsToSave)
        {
        }

        public override string ToString()
        {
            return string.Concat(settings.Value.Select(x => $"{x.Key}: {x.Value}\n"));
        }
    }
}