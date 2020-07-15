using System;
using System.Diagnostics.CodeAnalysis;

namespace Octopus.Shared.Configuration
{
    public interface IApplicationInstanceSelector
    {
        LoadedApplicationInstance GetCurrentInstance();
        void CreateDefaultInstance(string configurationFile, string? homeDirectory = null);
        void CreateInstance(string instanceName, string configurationFile, string? homeDirectory = null);
        void DeleteInstance();
        bool TryGetCurrentInstance([NotNullWhen(true)] out LoadedApplicationInstance? instance);
    }
}