using System;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceSelector
    {
        ApplicationName ApplicationName { get; }

        bool IsCurrentInstanceDefault();
        string? GetCurrentName();
        IKeyValueStore GetCurrentConfiguration();
        IWritableKeyValueStore GetWritableCurrentConfiguration();

        bool CanLoadCurrentInstance();
    }
}