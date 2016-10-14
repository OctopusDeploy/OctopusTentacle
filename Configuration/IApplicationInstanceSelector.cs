using System;

namespace Octopus.Shared.Configuration
{
    public interface IApplicationInstanceSelector
    {
        LoadedApplicationInstance Current { get; }
        void CreateDefaultInstance(string configurationFile);
        void CreateInstance(string instanceName, string configurationFile);
        void DeleteDefaultInstance();
        void DeleteInstance(string instanceName);
    }
}