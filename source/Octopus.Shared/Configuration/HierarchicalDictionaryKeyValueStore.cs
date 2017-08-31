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

        public override TData Get<TData>(string name, TData defaultValue, DataProtectionScope? protectionScope)
        {
            throw new NotImplementedException("This ");
        }

        public override void Set<TData>(string name, TData value, DataProtectionScope? protectionScope = null)
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

            if (protectionScope != null)
            {
                if (!(valueAsObject is string))
                    valueAsObject = JsonConvert.SerializeObject(value);
                var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes((string)valueAsObject), null, protectionScope.Value);
                valueAsObject = Convert.ToBase64String(protectedBytes);
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
