using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Octodiff.Core;
using Octopus.Shared.Util;
using Signature = System.Security.Cryptography.Xml.Signature;

namespace Octopus.Shared.Packages
{
    class BinaryFormat
    {
        public static readonly byte[] SignatureHeader = Encoding.ASCII.GetBytes("OCTOSIG");
        public static readonly byte[] DeltaHeader = Encoding.ASCII.GetBytes("OCTODELTA");
        public static readonly byte[] EndOfMetadata = Encoding.ASCII.GetBytes(">>>");
        public const byte CopyCommand = 0x60;
        public const byte DataCommand = 0x80;

        public const byte Version = 0x01;
    }

    class SignatureWriter : ISignatureWriter
    {
        private readonly BinaryWriter signatureStream;

        public SignatureWriter(Stream signatureStream)
        {
            this.signatureStream = new BinaryWriter(signatureStream);
        }

        public void WriteMetadata(IHashAlgorithm hashAlgorithm, IRollingChecksum rollingChecksumAlgorithm, byte[] hash)
        {
            signatureStream.Write(BinaryFormat.SignatureHeader);
            signatureStream.Write(BinaryFormat.Version);
            signatureStream.Write(hashAlgorithm.Name);
            signatureStream.Write(rollingChecksumAlgorithm.Name);
            signatureStream.Write(BinaryFormat.EndOfMetadata);
        }

        public void WriteChunk(ChunkSignature signature)
        {
            signatureStream.Write(signature.Length);
            signatureStream.Write(signature.RollingChecksum);
            signatureStream.Write(signature.Hash);
        }
    }

    public class PackageDeltaFactory : IPackageDeltaFactory
    {
        readonly IOctopusFileSystem fileSystem;

        public PackageDeltaFactory(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public Stream BuildSignature(StoredPackage nearestPackage)
        {
            var signatureFilePath = nearestPackage.FullPath + ".octosig";
            
            var signatureBuilder = new SignatureBuilder();
            using(var basisStream = fileSystem.OpenFile(nearestPackage.FullPath, FileAccess.Read))
            using (var signatureStream = fileSystem.OpenFile(signatureFilePath, FileMode.Create, FileAccess.Write))
            {
                signatureBuilder.Build(basisStream, new SignatureWriter(signatureStream));
            }

            return fileSystem.OpenFile(signatureFilePath, FileAccess.Read);
        }

        public Stream BuildDelta(Stream newPackage, Stream signatureStream, string deltaFilePath)
        {
            var delta = new DeltaBuilder();
            using (var deltaStream = fileSystem.OpenFile(deltaFilePath, FileMode.Create, FileAccess.Write))
            {
                delta.BuildDelta(newPackage, new SignatureReader(signatureStream, delta.ProgressReporter), new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(deltaStream)));
            }

            return fileSystem.OpenFile(deltaFilePath, FileAccess.Read);
        }
    }
}
