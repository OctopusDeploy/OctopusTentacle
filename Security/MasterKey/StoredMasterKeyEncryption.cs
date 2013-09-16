using System;
using System.IO;
using System.Security.Cryptography;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Model;
using Octopus.Platform.Security.MasterKey;
using Octopus.Shared.Bcl.IO;

namespace Octopus.Shared.Security.MasterKey
{
    public class StoredMasterKeyEncryption : IMasterKeyEncryption
    {
        readonly IOctopusServerStorageConfiguration storageConfiguration;

        public StoredMasterKeyEncryption(IOctopusServerStorageConfiguration storageConfiguration)
        {
            this.storageConfiguration = storageConfiguration;
        }

        byte[] MasterKey { get { return storageConfiguration.MasterKey; } }

        public EncryptedBytes ToCiphertext(byte[] plaintext)
        {
            return MasterKeyEncryption.ToCiphertext(MasterKey, plaintext);
        }

        public byte[] ToPlaintext(EncryptedBytes encrypted)
        {
            return MasterKeyEncryption.ToPlaintext(MasterKey, encrypted);
        }

        public Stream ReadAsCiphertext(Stream plaintext)
        {
            return MasterKeyEncryption.ReadAsCiphertext(MasterKey, plaintext);
        }

        public Stream ReadAsPlaintext(Stream ciphertext)
        {
            return MasterKeyEncryption.ReadAsPlaintext(MasterKey, ciphertext);
        }

        public Stream WriteCiphertextTo(Stream stream)
        {
            return MasterKeyEncryption.WriteCiphertextTo(MasterKey, stream);
        }
    }
}
