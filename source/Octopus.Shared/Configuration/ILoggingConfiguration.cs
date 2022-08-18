using System;

namespace Octopus.Shared.Configuration
{
    public interface ILoggingConfiguration
    {
        string LogsDirectory { get; }
    }
}