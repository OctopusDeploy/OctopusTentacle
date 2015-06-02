using System;
using System.IO;
using Octopus.Shared.Util;

namespace Octopus.Shared.Packages
{
    public interface IPackageDeltaFactory
    {
        string BuildSignature(string nearestPackageFilePath, ISemaphore semaphore);
        Stream BuildDelta(string newPackageFilePath, string signatureFilePath, string deltaFileName, ISemaphore semaphore);
    }
}