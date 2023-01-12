using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Configuration.Instances
{
    /// <summary>
    /// Named instances are registered on the machine in a centralized store that allows easier reference for starting, stopping or updating.
    /// These named instances are stored in a different place for both Windows and Linux but in both cases it is in a machine-wide location
    /// so that any user can reference an instance in a cli command by name directly.
    /// </summary>
    public interface IApplicationInstanceStore
    {
        bool TryLoadInstanceDetails(string? instanceName, out ApplicationInstanceRecord? instanceRecord);
        ApplicationInstanceRecord LoadInstanceDetails(string? instanceName);
        void RegisterInstance(ApplicationInstanceRecord instanceRecord);
        void DeleteInstance(string instanceName);
        IList<ApplicationInstanceRecord> ListInstances();
    }
}