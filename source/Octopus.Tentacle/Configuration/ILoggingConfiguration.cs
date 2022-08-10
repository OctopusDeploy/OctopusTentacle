using System;

namespace Octopus.Tentacle.Configuration
{
    public interface ILoggingConfiguration
    {
        string LogsDirectory { get; }
    }
}