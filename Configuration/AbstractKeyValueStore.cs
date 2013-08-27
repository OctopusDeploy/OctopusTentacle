using System;
using Octopus.Platform.Security;
using Octopus.Platform.Util;
using Octopus.Shared.Security;

namespace Octopus.Shared.Configuration
{
    public abstract class AbstractKeyValueStore : IKeyValueStore
    {
        static readonly EncryptionAlgorithm Algorithm = new Aes256EncryptionAlgorithm();

        protected abstract void Write(string key, string value);
        protected abstract string Read(string key);

        public TData Get<TData>(string name, TData defaultValue)
        {
            var value = Get(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return (TData)AmazingConverter.Convert(value, typeof(TData));
            }

            return defaultValue;
        }

        public void Set(string name, object value)
        {
            SetString(name, value == null ? null : value.ToString());
        }

        public void SetSecure(string name, object value)
        {
            var encrypted = Algorithm.Encrypt(value == null ? null : value.ToString());
            Set(name, encrypted.ToBase64());
        }

        public abstract void Save();

        public string GetSecure(string name)
        {
            var text = Get(name);
            if (text == null)
            {
                text = Algorithm.Encrypt(string.Empty).ToBase64();
            }

            var encrypted = EncryptResult.FromBase64(text);
            var decrypted = Algorithm.Decrypt(encrypted.CipherText, encrypted.Salt);
            return decrypted;
        }

        public string Get(string name)
        {
            return Read(name);
        }

        void SetString(string name, string value)
        {
            Write(name, value);
        }
    }
}