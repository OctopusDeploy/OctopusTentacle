using System;
using Newtonsoft.Json;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{
    public abstract class FlatDictionaryKeyValueStore : DictionaryKeyValueStore
    {
        protected FlatDictionaryKeyValueStore(bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
        }

        public override TData Get<TData>(string name, TData defaultValue, ProtectionLevel protectionLevel  = ProtectionLevel.None)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            string valueAsString = null;
            try
            {
                var data = Read(name);
                valueAsString = data as string;
                if (string.IsNullOrWhiteSpace(valueAsString))
                    return defaultValue;

                if (protectionLevel == ProtectionLevel.MachineKey)
                {
                    data = MachineKeyEncrypter.Current.Decrypt(valueAsString);
                }

                if (typeof(TData) == typeof(string))
                    return (TData) data;
                if (typeof(TData) == typeof(bool)) //bool is tricky - .NET uses 'True', whereas JSON uses 'true' - need to allow both, because UX/legacy
                    return (TData) (object) bool.Parse((string) data);

                return JsonConvert.DeserializeObject<TData>((string) data);
            }
            catch (Exception e)
            {
                if (protectionLevel == ProtectionLevel.None)
                    throw new FormatException($"Unable to parse configuration key '{name}' as a '{typeof(TData).Name}'. Value was '{valueAsString}'.", e);
                throw new FormatException($"Unable to parse configuration key '{name}' as a '{typeof(TData).Name}'.", e);
            }
        }
        
        public override void Set<TData>(string name, TData value, ProtectionLevel protectionLevel  = ProtectionLevel.None)
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

            if (ValueNeedsToBeSerialized(protectionLevel, valueAsObject))
                valueAsObject = JsonConvert.SerializeObject(value);

            if (protectionLevel == ProtectionLevel.MachineKey && valueAsObject != null)
            {
                valueAsObject = MachineKeyEncrypter.Current.Encrypt((string)valueAsObject);
            }

            Write(name, valueAsObject);
            if (AutoSaveOnSet)
                Save();
        }

        protected virtual bool ValueNeedsToBeSerialized(ProtectionLevel protectionLevel, object valueAsObject)
        {
            //null would end up as "null" rather than empty
            if (valueAsObject == null)
                return false;
            
            //bool/int/string etc will work fine directly when used as ToString()
            //custom types will end up as the object type instead of anything useful
            if (valueAsObject.GetType().ToString() == valueAsObject.ToString())
                return true;

            //dont stick extra quotes around a string
            if (valueAsObject is string)
                return false;

            //need to convert bool/int/etc to a string for it to be encrypted
            if (protectionLevel == ProtectionLevel.MachineKey)
                return true;

            return false;
        }

        private bool IsEmptyString(object value)
        {
            return value is string s && string.IsNullOrWhiteSpace(s);
        }
    }
}