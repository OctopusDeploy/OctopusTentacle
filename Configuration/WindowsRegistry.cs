using System;
using Microsoft.Win32;
using Octopus.Shared.Security;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Makes it easy to store key/value pairs in the Windows Registry.
    /// </summary>
    public class WindowsRegistry : IWindowsRegistry
    {
        static readonly EncryptionAlgorithm Algorithm = new Aes256EncryptionAlgorithm();
        const RegistryHive Hive = RegistryHive.LocalMachine;
        const RegistryView View = RegistryView.Registry64;
        const string KeyName = "Software\\Octopus";
        
        public TData Get<TData>(string name, TData defaultValue)
        {
            var value = GetString(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return (TData)Convert.ChangeType(value, typeof (TData));
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

        public string GetSecure(string name)
        {
            var text = GetString(name);
            if (text == null)
            {
                text = Algorithm.Encrypt(string.Empty).ToBase64();
            }

            var encrypted = EncryptResult.FromBase64(text);
            var decrypted = Algorithm.Decrypt(encrypted.CipherText, encrypted.Salt);
            return decrypted;
        }

        public string GetString(string name)
        {
            var key = RegistryKey.OpenBaseKey(Hive, View);
            key = key.OpenSubKey(KeyName, false);
            if (key != null)
            {
                return (string) key.GetValue(name, null);
            }

            return null;
        }

        void SetString(string name, string value)
        {
            var key = RegistryKey.OpenBaseKey(Hive, View);
            var octopusKey = key.OpenSubKey(KeyName, true);
            if (octopusKey == null)
            {
                key.CreateSubKey(KeyName);
                octopusKey = key.OpenSubKey(KeyName, true);
            }

            octopusKey.SetValue(name, value);
            octopusKey.Flush();
            octopusKey.Close();
        }
    }
}