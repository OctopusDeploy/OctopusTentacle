using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Configuration.Crypto
{
    /// <summary>
    /// Protects <see cref="ProtectionLevel.MachineKey"/> values on Linux.
    ///
    /// Values are encrypted with AES-256-CBC using the key from <see cref="LinuxGeneratedMachineKey"/> and a fresh
    /// IV for every value, and stored as
    /// <c>$OctopusMachineKeyV1$base64(iv || ciphertext)</c>.
    ///
    /// Values without that prefix were written by earlier versions of Tentacle, which used a key derived from
    /// <c>/etc/machine-id</c> (falling back to the generated key) and a fixed IV. They are still decrypted the way
    /// those versions decrypted them so that existing configuration keeps working after an upgrade, and
    /// <see cref="ProtectedSettingsMigrator"/> re-encrypts them with the current scheme when the agent starts.
    /// Nothing is ever encrypted with a legacy key any more.
    /// </summary>
    public class LinuxMachineKeyEncryptor : IMachineKeyEncryptor
    {
        /// <summary>
        /// Marks a value encrypted with the current scheme. <c>$</c> is not a base64 character, so a value without
        /// this prefix is unambiguously a legacy one. Must never change; bump the version number instead.
        /// </summary>
        public const string ProtectedValuePrefix = "$OctopusMachineKeyV1$";

        const int IvSizeInBytes = 16;

        // Any plaintext we ever encrypted is valid UTF-8, so a decryption that yields invalid UTF-8 was done with the
        // wrong key. Rejecting it here means a wrong legacy key is detected far more reliably than by the roughly
        // 1-in-256 chance that AES-CBC padding happens to validate.
        static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        readonly ISystemLog log;
        readonly ICryptoKeyNixSource keySource;
        readonly IReadOnlyList<ICryptoKeyNixSource> legacyKeySources;

        /// <param name="keySource">The key used to encrypt every new value and to decrypt values carrying <see cref="ProtectedValuePrefix"/>.</param>
        /// <param name="legacyKeySources">The keys, in the order earlier versions tried them, used only to decrypt values without the prefix.</param>
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

        public string Encrypt(string raw)
        {
            try
            {
                var (key, _) = keySource.Load();
                using var aes = Aes.Create();
                aes.GenerateIV();
                using var encryptor = aes.CreateEncryptor(key, aes.IV);
                var plaintext = Encoding.UTF8.GetBytes(raw);
                var ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

                var payload = new byte[aes.IV.Length + ciphertext.Length];
                Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
                Buffer.BlockCopy(ciphertext, 0, payload, aes.IV.Length, ciphertext.Length);

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
                if (payload.Length < IvSizeInBytes)
                    throw new FormatException("The encrypted value is too short to contain an IV.");

                var iv = new byte[IvSizeInBytes];
                Buffer.BlockCopy(payload, 0, iv, 0, IvSizeInBytes);

                var (key, _) = keySource.Load();
                using var aes = Aes.Create();
                using var decryptor = aes.CreateDecryptor(key, iv);
                var plaintext = decryptor.TransformFinalBlock(payload, IvSizeInBytes, payload.Length - IvSizeInBytes);
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

        /// <remarks>
        /// This is exactly what earlier versions did on read, and it must stay that way so that configuration written
        /// by those versions keeps decrypting after an upgrade.
        /// </remarks>
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

            throw new AggregateException("Unable to decrypt a value that was protected by an earlier version of Tentacle with any of the keys it could have used. "
                + "Earlier versions derived the key from /etc/machine-id, so this happens when the configuration was moved from another machine, "
                + "or is on a volume mounted into a container whose image has since changed. "
                + "Run 'tentacle new-certificate' and re-establish trust with the Octopus Server, or restore the configuration file from a backup taken on the original machine.", errors);
        }
    }
}
