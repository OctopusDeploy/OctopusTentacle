using System;

namespace Octopus.Shared.Configuration
{
    public interface IApplicationInstanceSelector
    {
        void LoadDefaultInstance();
        void LoadInstance(string instanceName);
        void CreateDefaultInstance(string configurationFile);
        void CreateInstance(string instanceName, string configurationFile);
        void DeleteDefaultInstance();
        void DeleteInstance(string instanceName);

        LoadedApplicationInstance Current { get; }
    }
}