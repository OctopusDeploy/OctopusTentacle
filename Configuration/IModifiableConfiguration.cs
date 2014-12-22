using System;

namespace Octopus.Platform.Deployment.Configuration
{
    public interface IModifiableConfiguration
    {
        void Save();
    }
}