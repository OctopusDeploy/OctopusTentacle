using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Configuration.Crypto
{
    // Stored as $OctopusMachineKeyV1$base64(nonce || ciphertext || tag), AES-256-GCM with the prefix as associated data.
    // Values without the prefix were written by earlier versions (unauthenticated AES-CBC, machine-id or generated key,
    // fixed IV); they are still decrypted that way, and ProtectedSettingsMigrator rewrites them on agent start.
    public class LinuxMachineKeyEncryptor : IMachineKeyEncryptor
    {
        // $ is not a base64 character, so a value without this prefix is unambiguously a legacy one. Never change this;
        // bump the version number instead.
        public const string ProtectedValuePrefix = "$OctopusMachineKeyV1$";

        public const int KeySizeInBytes = 32;
        public const int NonceSizeInBytes = 12;
        public const int TagSizeInBytes = 16;

        static readonly byte[] AssociatedData = Encoding.ASCII.GetBytes(ProtectedValuePrefix);

        // Any plaintext we ever encrypted is valid UTF-8, so a legacy decryption that yields invalid UTF-8 was done with
        // the wrong key. Rejecting it means a wrong legacy key is detected far more reliably than by the roughly
        // 1-in-256 chance that AES-CBC padding happens to validate.
        static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        readonly ISystemLog log;
        readonly ICryptoKeyNixSource keySource;
        readonly IReadOnlyList<ICryptoKeyNixSource> legacyKeySources;

        // legacyKeySources are tried in the order earlier versions tried them, and only for values without the prefix.
        public LinuxMachineKeyEncryptor(ISystemLog log, ICryptoKeyNixSource keySource, IEnumerable<ICryptoKeyNixSource> legacyKeySources)
        {
            this.log = log;
            this.keySource = keySource;
            this.legacyKeySources = legacyKeySources.ToArray();
        }

        public static bool IsLegacyCiphertext(string encrypted)
            => !encrypted.StartsWith(ProtectedValuePrefix, StringComparison.Ordinal);

        public bool RequiresReEncryption(string encrypted)
            => IsLegacyCiphertext(encrypted);

        public void RestrictKeyStorageToOwner()
        {
            if (keySource is LinuxGeneratedMachineKey generatedKey)
                generatedKey.RestrictPermissionsToOwner();
        }

        public string Encrypt(string raw)
        {
            try
            {
                var key = LoadCurrentKey();
                var plaintext = Encoding.UTF8.GetBytes(raw);
                var payload = new byte[NonceSizeInBytes + plaintext.Length + TagSizeInBytes];
                using (var random = RandomNumberGenerator.Create())
                {
                    var nonce = new byte[NonceSizeInBytes];
                    random.GetBytes(nonce);
                    Buffer.BlockCopy(nonce, 0, payload, 0, NonceSizeInBytes);
                }

                Gcm.Encrypt(key, plaintext, payload, AssociatedData);

                return ProtectedValuePrefix + Convert.ToBase64String(payload);
            }
            catch (Exception e)
            {
                throw new CryptographicException($"Unable to encrypt a value with the Tentacle machine key: {e.Message}", e);
            }
        }

        public string Decrypt(string encrypted)
            => IsLegacyCiphertext(encrypted)
                ? DecryptLegacy(encrypted)
                : DecryptCurrent(encrypted);

        string DecryptCurrent(string encrypted)
        {
            try
            {
                var payload = Convert.FromBase64String(encrypted.Substring(ProtectedValuePrefix.Length));
                if (payload.Length < NonceSizeInBytes + TagSizeInBytes)
                    throw new FormatException("The encrypted value is too short to contain a nonce and an authentication tag.");

                var plaintext = Gcm.Decrypt(LoadCurrentKey(), payload, AssociatedData);
                return StrictUtf8.GetString(plaintext);
            }
            catch (Exception e)
            {
                // Deliberately not falling back to the legacy keys: a value with the prefix was written with the
                // current key, so anything else is the wrong key and must not be allowed to "succeed" by chance.
                throw new CryptographicException("Unable to decrypt a value protected with the Tentacle machine key. "
                    + "Either the value has been altered, or the machine key file it was encrypted with has been replaced or removed. "
                    + $"Details: {e.Message}", e);
            }
        }

        byte[] LoadCurrentKey()
        {
            var (key, _) = keySource.Load();
            if (key.Length != KeySizeInBytes)
                throw new CryptographicException($"The machine key must be {KeySizeInBytes * 8} bits, but is {key.Length * 8}.");
            return key;
        }

        // Exactly what earlier versions did on read; it must stay that way so their configuration keeps decrypting after an upgrade.
        string DecryptLegacy(string encrypted)
        {
            var errors = new List<Exception>();
            foreach (var source in legacyKeySources)
            {
                try
                {
                    var (key, iv) = source.Load();
                    using var aes = Aes.Create();
                    using var decryptor = aes.CreateDecryptor(key, iv);
                    var payload = Convert.FromBase64String(encrypted);
                    var plaintext = decryptor.TransformFinalBlock(payload, 0, payload.Length);
                    return StrictUtf8.GetString(plaintext);
                }
                catch (Exception e)
                {
                    log.Verbose(e.Message);
                    errors.Add(e);
                }
            }

            // A CryptographicException carrying the guidance, rather than an AggregateException, whose message the console
            // replaces with "Aggregate Exception". The individual key failures are kept as the inner exception.
            throw new CryptographicException("Unable to decrypt a value that was protected by an earlier version of Tentacle with any of the keys it could have used. "
                + "Earlier versions derived the key from /etc/machine-id, so this happens when the configuration was moved from another machine, "
                + "or is on a volume mounted into a container whose image has since changed. "
                + "If the machine-id it was written with is still available (for a container, `docker run --rm --entrypoint cat <previous image> /etc/machine-id`), "
                + "make it available read-only at /etc/machine-id for one start and Tentacle will re-encrypt the configuration with its own key. "
                + "Otherwise run 'tentacle new-certificate' and re-establish trust with the Octopus Server, or restore the configuration file from a backup taken on the original machine.",
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
                    throw new PlatformNotSupportedException("AES-GCM is not available on this platform.");
            }
#endif
        }
    }
}
