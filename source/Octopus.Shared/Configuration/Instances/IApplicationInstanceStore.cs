using System;

namespace Octopus.Shared.Configuration.Instances
{
    internal interface IApplicationInstanceStore :  IApplicationInstanceLocator
    {
        void SaveInstance(ApplicationInstanceRecord instanceRecord);

        void DeleteInstance(string instanceName);

        void MigrateInstance(ApplicationInstanceRecord instanceRecord);
    }
}