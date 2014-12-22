using System;
using NuGet;

namespace Octopus.Shared.BuiltInFeed
{
    public interface IBuiltInPackageRepositoryFactory
    {
        bool IsBuiltInSource(string packageSource);
        IPackageRepository CreateRepository();
    }
}
