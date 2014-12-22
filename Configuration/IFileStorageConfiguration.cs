using System;

namespace Octopus.Shared.Configuration
{
    public interface IFileStorageConfiguration
    {
        string FileStorageDirectory { get; }
    }
}
