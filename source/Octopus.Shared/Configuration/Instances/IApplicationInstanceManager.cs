using System;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IApplicationInstanceManager
    {
        ApplicationInstanceRecord? GetInstance(string instanceName);
        void CreateDefaultInstance(string configurationFile, string? homeDirectory = null);
        void CreateInstance(string instanceName, string configurationFile, string? homeDirectory = null);
        void DeleteInstance(string instanceName);
    }
}