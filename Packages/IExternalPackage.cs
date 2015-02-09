using System;
using NuGet;

namespace Octopus.Shared.Packages
{
    public interface IExternalPackage : INuGetPackage
    {
        IPackage GetRealPackage();
    }
}