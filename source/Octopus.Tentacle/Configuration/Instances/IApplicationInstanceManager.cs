using System;

namespace Octopus.Tentacle.Configuration.Instances
{
    /// <summary>
    /// Coordinates creation and deletion of an instance
    /// </summary>
    public interface IApplicationInstanceManager
    {
        void CreateDefaultInstance(string configurationFile, string? homeDirectory = null);
        void CreateInstance(string instanceName, string configurationFile, string? homeDirectory = null);
        void DeleteInstance(string instanceName);
    }
}