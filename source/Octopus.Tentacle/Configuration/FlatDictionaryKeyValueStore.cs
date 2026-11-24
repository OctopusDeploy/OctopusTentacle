using System;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Tentacle.Configuration
{
    public abstract class FlatDictionaryKeyValueStore : DictionaryKeyValueStore, IAggregatableKeyValueStore
    {
        protected readonly JsonSerializerSettings JsonSerializerSettings;
        protected readonly IMachineKeyEncryptor Encryptor;

        protected FlatDictionaryKeyValueStore(JsonSerializerSettings jsonSerializerSettings, IMachineKeyEncryptor encryptor, bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
            JsonSerializerSettings = jsonSerializerSettings;
            Encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
        }

        public override TData? Get<TData>(string name, TData? defaultValue = default, ProtectionLevel protectionLevel = ProtectionLevel.None) where TData : default
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (Read(name) is not string valueAsString || string.IsNullOrWhiteSpace(valueAsString))
            {
                return defaultValue;
            }

            if (protectionLevel == ProtectionLevel.MachineKey)
            {
                valueAsString = Decrypt(name, valueAsString);
            }

            return Parse<TData>(name, valueAsString, protectionLevel);
        }

        public (bool foundResult, TData? value) TryGet<TData>(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (Read(name) is not string valueAsString || string.IsNullOrWhiteSpace(valueAsString))
            {
                return (false, default!);
            }

            if (protectionLevel == ProtectionLevel.MachineKey)
            {
                valueAsString = Decrypt(name, valueAsString);
            }

            return (true, Parse<TData>(name, valueAsString, protectionLevel));
        }

        TData? Parse<TData>(string name, string value, ProtectionLevel protectionLevel)
        {
            try
            {
                if (typeof(TData) == typeof(string))
                {
                    return (TData)(object)value;
                }

                if (typeof(TData) == typeof(bool)) //bool is tricky - .NET uses 'True', whereas JSON uses 'true' - need to allow both, because UX/legacy
                {
                    return (TData)(object)bool.Parse(value);
                }

                if (typeof(TData).IsEnum)
                {
                    return (TData)Enum.Parse(typeof(TData), value.Trim('"'));
                }

                return JsonConvert.DeserializeObject<TData>(value, JsonSerializerSettings);
            }
            catch (Exception e)
            {
                if (protectionLevel == ProtectionLevel.None)
                {
                    throw new FormatException($"Unable to parse configuration key '{name}' as a '{typeof(TData).Name}'. Value was '{value}'.", e);
                }

                throw new FormatException($"Unable to parse configuration key '{name}' as a '{typeof(TData).Name}'.", e);
            }
        }

        string Decrypt(string name, string value)
        {
            try
            {
                return Encryptor.Decrypt(value);
            }
            catch (Exception e)
            {
                throw new ProtectedSettingDecryptionException(name, e);
            }
        }

        public override bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (value == null || value is string s && string.IsNullOrWhiteSpace(s))
            {
                Write(name, null);
                if (AutoSaveOnSet)
                    return Save();
                return true;
            }

            var valueAsObject = (object)value;

            if (ValueNeedsToBeSerialized(protectionLevel, valueAsObject))
                valueAsObject = JsonConvert.SerializeObject(value, JsonSerializerSettings);

            if (protectionLevel == ProtectionLevel.MachineKey && valueAsObject != null)
                valueAsObject = EncryptWithoutDowngrading(name, (string)valueAsObject);

            Write(name, valueAsObject);
            if (AutoSaveOnSet)
                return Save();

            return true;
        }

        string EncryptWithoutDowngrading(string name, string raw)
        {
            var encrypted = Encryptor.Encrypt(raw);
            if (Encryptor.RequiresReEncryption(encrypted) && Read(name) is string previous && !Encryptor.RequiresReEncryption(previous))
            {
                throw new CryptographicException($"Unable to update the protected setting '{name}': it is protected with the configuration encryption key, which is not available. Make sure the key file exists and is readable.");
            }
            return encrypted;
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
    }
}