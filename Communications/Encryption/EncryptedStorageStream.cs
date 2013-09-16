using System;
using System.IO;
using Octopus.Platform.Security.MasterKey;
using Pipefish.Util.Storage;

namespace Octopus.Shared.Communications.Encryption
{
    public class EncryptedStorageStream : IStorageStreamTransform
    {
        readonly IMasterKeyEncryption encryption;

        public EncryptedStorageStream(IMasterKeyEncryption encryption)
        {
            this.encryption = encryption;
        }

        public Stream CreateReadStream(Stream storage)
        {
            return encryption.ReadAsPlaintext(storage);
        }

        public Stream CreateWriteStream(Stream storage)
        {
            return encryption.WriteCiphertextTo(storage);
        }
    }
}
