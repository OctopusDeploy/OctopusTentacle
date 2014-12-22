using System;
using NuGet;

namespace Octopus.Platform.Deployment.BuiltInFeed
{
    public interface IBuiltInPackageRepositoryFactory
    {
        bool IsBuiltInSource(string packageSource);
        IPackageRepository CreateRepository();
    }
}
