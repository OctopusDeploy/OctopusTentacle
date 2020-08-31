using System.Collections.Generic;
using System.Linq;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    public class EnvFileInstanceStrategy : IVirtualApplicationInstanceStrategy
    {
        const string EnvFileBasedInstanceName = "EnvFileInstance";

        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly IOctopusFileSystem fileSystem;
        readonly IEnvFileLocator envFileLocator;

        public EnvFileInstanceStrategy(StartUpInstanceRequest startUpInstanceRequest, IOctopusFileSystem fileSystem, IEnvFileLocator envFileLocator)
        {
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.fileSystem = fileSystem;
            this.envFileLocator = envFileLocator;
        }

        public int Priority => 100;

        public bool AnyInstancesConfigured()
        {
            return startUpInstanceRequest is StartUpDynamicInstanceRequest && envFileLocator.LocateEnvFile() != null;
        }

        public IList<ApplicationInstanceRecord> ListInstances()
        {
            if (!AnyInstancesConfigured())
                return Enumerable.Empty<ApplicationInstanceRecord>().ToList();
            return new List<ApplicationInstanceRecord>();
        }

        public LoadedApplicationInstance LoadedApplicationInstance(ApplicationInstanceRecord applicationInstance)
        {
            return new LoadedApplicationInstance(applicationInstance.InstanceName, new EnvBasedKeyValueStore(fileSystem, envFileLocator));
        }

        public ApplicationInstanceRecord? GetInstance()
        {
            if (!AnyInstancesConfigured())
                return null;
            return new ApplicationInstanceRecord(EnvFileBasedInstanceName, true);
        }
    }
}