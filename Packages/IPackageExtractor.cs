using System;

namespace Octopus.Shared.Packages
{
    public interface IPackageExtractor
    {
        void Install(string packageFile, string directory);
    }
}