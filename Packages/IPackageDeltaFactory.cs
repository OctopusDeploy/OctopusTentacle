using System.IO;

namespace Octopus.Shared.Packages
{
    public interface IPackageDeltaFactory
    {
        string BuildSignature(string nearestPackageFilePath);
        Stream BuildDelta(string newPackageFilePath, string signatureFilePath, string deltaFilePath);
    }
}