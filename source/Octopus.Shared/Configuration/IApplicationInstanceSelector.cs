using System;

namespace Octopus.Shared.Configuration
{
    public interface IApplicationInstanceSelector
    {
        LoadedApplicationInstance GetCurrentInstance();
        void CreateDefaultInstance(string configurationFile, string homeDirectory = null);
        void CreateInstance(string instanceName, string configurationFile, string homeDirectory = null);
        void DeleteInstance();
        bool TryGetCurrentInstance(out LoadedApplicationInstance instance);
    }
}