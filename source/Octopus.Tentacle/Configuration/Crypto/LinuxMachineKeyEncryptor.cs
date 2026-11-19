using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Configuration.Crypto
{
    // Stored as AES256-GCM$base64(nonce)$base64(ciphertext || tag), with the specifier as associated data.
    // Values without a $ were written by earlier versions with AES-CBC and a fixed IV.
    public class LinuxMachineKeyEncryptor : IMachineKeyEncryptor
    {
        public const string Specifier = "AES256-GCM";
        // $ is not a base64 character, so a value without one is unambiguously a legacy one.
        const char FieldSeparator = '$';

        const int NonceSizeInBytes = 12;
        const int TagSizeInBytes = 16;

        static readonly byte[] AssociatedData = Encoding.ASCII.GetBytes(Specifier);

        // A wrong legacy key passes the AES-CBC padding check about 1 time in 256; the garbage it yields is rarely valid UTF-8.
        static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        readonly ISystemLog log;
        readonly ConfigurationEncryptionKey configurationKey;
        readonly IReadOnlyList<ICryptoKeyNixSource> legacyKeySources;

        public LinuxMachineKeyEncryptor(ISystemLog log, ConfigurationEncryptionKey configurationKey, IEnumerable<ICryptoKeyNixSource> legacyKeySources)
        {
            this.log = log;
            this.configurationKey = configurationKey;
            this.legacyKeySources = legacyKeySources.ToArray();
        }

        static bool IsLegacyCiphertext(string encrypted)
            => encrypted.IndexOf(FieldSeparator) < 0;

        public bool RequiresReEncryption(string encrypted)
            => IsLegacyCiphertext(encrypted);

        // Until the configuration upgrade has created the key, values are written the way earlier versions wrote them.
        public string Encrypt(string raw)
        {
            try
            {
                var currentKey = configurationKey.TryLoad();
                return currentKey == null ? EncryptLegacy(raw) : EncryptCurrent(currentKey, raw);
            }
            catch (Exception e)
            {
                throw new CryptographicException($"Unable to encrypt a value with the Tentacle machine key: {e.Message}", e);
            }
        }

        static string EncryptCurrent(byte[] key, string raw)
        {
            var plaintext = Encoding.UTF8.GetBytes(raw);
            var payload = new byte[NonceSizeInBytes + plaintext.Length + TagSizeInBytes];
            using (var random = RandomNumberGenerator.Create())
            {
                var nonce = new byte[NonceSizeInBytes];
                random.GetBytes(nonce);
                Buffer.BlockCopy(nonce, 0, payload, 0, NonceSizeInBytes);
            }

            Gcm.Encrypt(key, plaintext, payload, AssociatedData);

            var nonceBase64 = Convert.ToBase64String(payload, 0, NonceSizeInBytes);
            var ciphertextBase64 = Convert.ToBase64String(payload, NonceSizeInBytes, payload.Length - NonceSizeInBytes);
            return string.Join(FieldSeparator.ToString(), Specifier, nonceBase64, ciphertextBase64);
        }

        string EncryptLegacy(string raw)
        {
            var (encrypted, errors) = TryEachLegacyKey(legacy =>
            {
                using var aes = Aes.Create();
                using var encryptor = aes.CreateEncryptor(legacy.Key, legacy.IV);
                var plaintext = Encoding.UTF8.GetBytes(raw);
                return Convert.ToBase64String(encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length));
            });

            return encrypted ?? throw new AggregateException("There is no configuration encryption key yet, and none of the keys earlier versions used is available.", errors);
        }

        (string? Result, List<Exception> Errors) TryEachLegacyKey(Func<(byte[] Key, byte[] IV), string> use)
        {
            var errors = new List<Exception>();
            foreach (var source in legacyKeySources)
            {
                try
                {
                    return (use(source.Load()), errors);
                }
                catch (Exception e)
                {
                    log.Verbose(e.Message);
                    errors.Add(e);
                }
            }

            return (null, errors);
        }

        public string Decrypt(string encrypted)
            => IsLegacyCiphertext(encrypted)
                ? DecryptLegacy(encrypted)
                : DecryptCurrent(encrypted);

        string DecryptCurrent(string encrypted)
        {
            byte[] key;
            try
            {
                key = configurationKey.Load();
            }
            catch (Exception e)
            {
                throw new CryptographicException(e.Message, e);
            }

            try
            {
                var payload = ParsePayload(encrypted);

                var plaintext = Gcm.Decrypt(key, payload, AssociatedData);
                return StrictUtf8.GetString(plaintext);
            }
            catch (Exception e)
            {
                // No fallback to the legacy keys: they could never have written a value in this format.
                throw new CryptographicException("Unable to decrypt a value protected with the Tentacle machine key. "
                    + "Either the value has been altered, or the machine key file it was encrypted with has been replaced or removed. "
                    + $"Details: {e.Message}", e);
            }
        }

        static byte[] ParsePayload(string encrypted)
        {
            var fields = encrypted.Split(FieldSeparator);
            if (fields.Length != 3)
            {
                throw new FormatException($"Expected 3 '{FieldSeparator}'-separated fields but found {fields.Length}.");
            }

            if (fields[0] != Specifier)
            {
                throw new FormatException($"Unknown encryption specifier '{fields[0]}'; expected '{Specifier}'.");
            }

            var nonce = Convert.FromBase64String(fields[1]);
            var ciphertextAndTag = Convert.FromBase64String(fields[2]);
            if (nonce.Length != NonceSizeInBytes || ciphertextAndTag.Length < TagSizeInBytes)
            {
                throw new FormatException("The nonce or authentication tag has the wrong length.");
            }

            return nonce.Concat(ciphertextAndTag).ToArray();
        }

        string DecryptLegacy(string encrypted)
        {
            var (decrypted, errors) = TryEachLegacyKey(legacy =>
            {
                using var aes = Aes.Create();
                using var decryptor = aes.CreateDecryptor(legacy.Key, legacy.IV);
                var payload = Convert.FromBase64String(encrypted);
                var plaintext = decryptor.TransformFinalBlock(payload, 0, payload.Length);
                return StrictUtf8.GetString(plaintext);
            });

            if (decrypted != null)
            {
                return decrypted;
            }

            throw new CryptographicException("Unable to decrypt a value protected by an earlier version of Tentacle. "
                + "Earlier versions derived the key from /etc/machine-id, so this happens when the configuration came from another machine or the machine-id has changed. "
                + "If the /etc/machine-id it was written with is still available, put it back at /etc/machine-id for one start and Tentacle will re-encrypt the configuration with its own key. "
                + "Otherwise restore the configuration file from a backup taken on the original machine, or run 'tentacle replace-certificate' to create a new certificate and update the thumbprint Octopus Server trusts.",
                new AggregateException(errors));
        }

        // Only reachable on .NET (Core): the .NET Framework build only runs on Windows, which uses DPAPI instead.
        static class Gcm
        {
            public static void Encrypt(byte[] key, byte[] plaintext, byte[] payload, byte[] associatedData)
            {
#if NETFRAMEWORK
                throw new PlatformNotSupportedException("The Linux machine key scheme is not available on .NET Framework.");
#else
                EnsureSupported();
                using var aes = new AesGcm(key, TagSizeInBytes);
                aes.Encrypt(payload.AsSpan(0, NonceSizeInBytes),
                    plaintext,
                    payload.AsSpan(NonceSizeInBytes, plaintext.Length),
                    payload.AsSpan(NonceSizeInBytes + plaintext.Length, TagSizeInBytes),
                    associatedData);
#endif
            }

            public static byte[] Decrypt(byte[] key, byte[] payload, byte[] associatedData)
            {
#if NETFRAMEWORK
                throw new PlatformNotSupportedException("The Linux machine key scheme is not available on .NET Framework.");
#else
                EnsureSupported();
                var ciphertextLength = payload.Length - NonceSizeInBytes - TagSizeInBytes;
                var plaintext = new byte[ciphertextLength];
                using var aes = new AesGcm(key, TagSizeInBytes);
                aes.Decrypt(payload.AsSpan(0, NonceSizeInBytes),
                    payload.AsSpan(NonceSizeInBytes, ciphertextLength),
                    payload.AsSpan(NonceSizeInBytes + ciphertextLength, TagSizeInBytes),
                    plaintext,
                    associatedData);
                return plaintext;
#endif
            }

#if !NETFRAMEWORK
            static void EnsureSupported()
            {
                if (!AesGcm.IsSupported)
                {
                    throw new PlatformNotSupportedException("AES-GCM is not available on this platform.");
                }
            }
#endif
        }
    }
}
