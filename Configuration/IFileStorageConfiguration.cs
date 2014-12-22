using System;

namespace Octopus.Platform.Deployment.Configuration
{
    public interface IFileStorageConfiguration
    {
        string FileStorageDirectory { get; }
    }
}
