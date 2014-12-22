using System;

namespace Octopus.Platform.Deployment.Configuration
{
    public interface ILoggingConfiguration
    {
        string LogsDirectory { get; }
    }
}