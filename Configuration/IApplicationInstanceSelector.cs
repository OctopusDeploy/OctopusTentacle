using System;

namespace Octopus.Shared.Configuration
{
    public interface IApplicationInstanceSelector
    {
        LoadedApplicationInstance Current { get; }
        void CreateDefaultInstance(string configurationFile, string homeDirectory = null);
        void CreateInstance(string instanceName, string configurationFile, string homeDirectory = null);
        void DeleteDefaultInstance();
        void DeleteInstance(string instanceName);
        bool TryLoadCurrentInstance(out LoadedApplicationInstance instance);
    }
}