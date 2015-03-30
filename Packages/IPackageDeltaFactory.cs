using System.IO;

namespace Octopus.Shared.Packages
{
    public interface IPackageDeltaFactory
    {
        Stream BuildSignature(StoredPackage nearestPackage);
        Stream BuildDelta(Stream newPackage, Stream signatureStream, string deltaFilePath);
    }
}