using System;
using Octopus.Shared.Packages;

namespace Octopus.Shared.BuiltInFeed
{
    public interface IBuiltInPackageRepositoryFactory
    {
        bool IsBuiltInSource(string packageSource);
        INuGetRepository CreateRepository();
    }
}
