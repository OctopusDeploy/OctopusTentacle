using System;
using Newtonsoft.Json;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Tentacle.Configuration
{
    public abstract class FlatDictionaryKeyValueStore : DictionaryKeyValueStore, IAggregatableKeyValueStore, IReEncryptingKeyValueStore
    {
        protected readonly JsonSerializerSettings JsonSerializerSettings;
        protected readonly IMachineKeyEncryptor Encryptor;

        /// <param name="encryptor">
        /// Protects <see cref="ProtectionLevel.MachineKey"/> values. Always the encryptor for the store's own file (see
        /// <see cref="MachineKeyEncryptor.ForConfigurationFile"/>): there is deliberately no default, so a store can never
        /// be built with a key other than the one its file is read back with.
        /// </param>
        protected FlatDictionaryKeyValueStore(JsonSerializerSettings jsonSerializerSettings, IMachineKeyEncryptor encryptor, bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
            JsonSerializerSettings = jsonSerializerSettings;
            Encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
        }

        public override TData? Get<TData>(string name, TData? defaultValue = default, ProtectionLevel protectionLevel = ProtectionLevel.None) where TData : default
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            string? valueAsString = null;
            var decrypting = false;
            try
            {
                var data = Read(name);
                if (data == null)
                    return defaultValue;
                valueAsString = data as string;
                if (valueAsString == null || string.IsNullOrWhiteSpace(valueAsString))
                    return defaultValue;

                if (protectionLevel == ProtectionLevel.MachineKey)
                {
                    decrypting = true;
                    data = Encryptor.Decrypt(valueAsString);
                    decrypting = false;
                }

                if (typeof(TData) == typeof(string))
                    return (TData)data;
                if (typeof(TData) == typeof(bool)) //bool is tricky - .NET uses 'True', whereas JSON uses 'true' - need to allow both, because UX/legacy
                    return (TData)(object)bool.Parse((string)data);
                if (typeof(TData).IsEnum)
                    return (TData)Enum.Parse(typeof(TData), ((string)data).Trim('"'));

                return JsonConvert.DeserializeObject<TData>((string)data, JsonSerializerSettings);
            }
            catch (Exception e)
            {
                // The reason is in the message itself, not just the inner exception, because it is what tells an
                // operator how to recover and the console does not always print inner messages.
                if (decrypting)
                    throw new FormatException($"Unable to decrypt the protected configuration setting '{name}'. {e.Message}", e);
                if (protectionLevel == ProtectionLevel.None)
                    throw new FormatException($"Unable to parse configuration key '{name}' as a '{typeof(TData).Name}'. Value was '{valueAsString}'.", e);
                throw new FormatException($"Unable to parse configuration key '{name}' as a '{typeof(TData).Name}'.", e);
            }
        }

        public (bool foundResult, TData? value) TryGet<TData>(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            string? valueAsString = null;
            var decrypting = false;
            try
            {
                var data = Read(name);
                if (data == null)
                    return (false, default!);
                valueAsString = data as string;
                if (valueAsString == null || string.IsNullOrWhiteSpace(valueAsString))
                    return (false, default!);

                if (protectionLevel == ProtectionLevel.MachineKey)
                {
                    decrypting = true;
                    data = Encryptor.Decrypt(valueAsString);
                    decrypting = false;
                }

                if (typeof(TData) == typeof(string))
                    return (true, (TData)data);
                if (typeof(TData) == typeof(bool)) //bool is tricky - .NET uses 'True', whereas JSON uses 'true' - need to allow both, because UX/legacy
                    return (true, (TData)(object)bool.Parse((string)data));
                if (typeof(TData).IsEnum)
                    return (true, (TData)Enum.Parse(typeof(TData), ((string)data).Trim('"')));

                return (true, JsonConvert.DeserializeObject<TData>((string)data, JsonSerializerSettings));
            }
            catch (Exception e)
            {
                // The reason is in the message itself, not just the inner exception, because it is what tells an
                // operator how to recover and the console does not always print inner messages.
                if (decrypting)
                    throw new FormatException($"Unable to decrypt the protected configuration setting '{name}'. {e.Message}", e);
                if (protectionLevel == ProtectionLevel.None)
                    throw new FormatException($"Unable to parse configuration key '{name}' as a '{typeof(TData).Name}'. Value was '{valueAsString}'.", e);
                throw new FormatException($"Unable to parse configuration key '{name}' as a '{typeof(TData).Name}'.", e);
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
                valueAsObject = Encryptor.Encrypt((string)valueAsObject);

            Write(name, valueAsObject);
            if (AutoSaveOnSet)
                return Save();

            return true;
        }

        public bool ReEncryptIfLegacy(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            var stored = Read(name) as string;
            if (stored == null || string.IsNullOrWhiteSpace(stored) || !Encryptor.RequiresReEncryption(stored))
                return false;

            // The stored form is the (possibly JSON-serialised) string that Set encrypted, so re-encrypting it as-is
            // preserves exactly what Get will deserialise afterwards.
            var reEncrypted = Encryptor.Encrypt(Encryptor.Decrypt(stored));

            BeforeReEncrypting();
            Write(name, reEncrypted);
            if (AutoSaveOnSet)
                Save();

            return true;
        }

        public virtual void RestrictKeyStorageToOwner()
            => Encryptor.RestrictKeyStorageToOwner();

        /// <summary>
        /// Called before <see cref="ReEncryptIfLegacy"/> rewrites a value, once the new value has been worked out. A
        /// store can use it to keep what an earlier version wrote; throwing leaves the stored value as it was.
        /// </summary>
        protected virtual void BeforeReEncrypting()
        {
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