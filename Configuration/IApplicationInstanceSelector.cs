using System;

namespace Octopus.Shared.Configuration
{
    public interface IApplicationInstanceSelector
    {
        LoadedApplicationInstance Current { get; }
        void LoadDefaultInstance();
        void LoadInstance(string instanceName);
        void CreateDefaultInstance(string configurationFile);
        void CreateInstance(string instanceName, string configurationFile);
        void DeleteDefaultInstance();
        void DeleteInstance(string instanceName);
        event Action Loaded;
    }
}