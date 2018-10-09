using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Octopus.Shared.Configuration
{
    public abstract class HierarchicalDictionaryKeyValueStore : DictionaryKeyValueStore
    {
        protected HierarchicalDictionaryKeyValueStore(bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
        }

        public override TData Get<TData>(string name, TData defaultValue, bool? machineKeyEncrypted = false)
        {
            throw new NotImplementedException("This ");
        }

        public override void Set<TData>(string name, TData value, bool? machineKeyEncrypted = false)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (IsEmptyString(value))
            {
                Write(name, null);
                if (AutoSaveOnSet)
                    Save();
                return;
            }

            var valueAsObject = (object) value;

            if (machineKeyEncrypted != null)
            {
                if (!(valueAsObject is string))
                    valueAsObject = JsonConvert.SerializeObject(value);
                valueAsObject = MachineKeyEncrypter.Current.Encrypt((string)valueAsObject);
            }

            Write(name, valueAsObject);
            if (AutoSaveOnSet)
                Save();
        }

        private bool IsEmptyString(object value)
        {
            return value is string s && string.IsNullOrWhiteSpace(s);
        }
    }
}
