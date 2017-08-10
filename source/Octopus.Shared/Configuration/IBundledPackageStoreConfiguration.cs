using System;

namespace Octopus.Shared.Configuration
{
    public interface IBundledPackageStoreConfiguration
    {
        string CustomPackageDirectory { get; set; }
    }
}