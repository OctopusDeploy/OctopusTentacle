using System;

namespace Octopus.Platform.Deployment.Configuration
{
    public interface IHomeConfiguration : IModifiableConfiguration
    {
        string ApplicationSpecificHomeDirectory { get; }
        string HomeDirectory { get; set; }
    }
}