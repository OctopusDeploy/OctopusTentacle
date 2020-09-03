using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;

namespace Octopus.Shared.Configuration.Instances
{
    public class EnvironmentInstanceStrategy : IApplicationInstanceStrategy
    {
        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly IMapEnvironmentVariablesToConfigItems mapper;
        readonly IEnvironmentVariableReader reader;

        public EnvironmentInstanceStrategy(StartUpInstanceRequest startUpInstanceRequest, IMapEnvironmentVariablesToConfigItems mapper, IEnvironmentVariableReader reader)
        {
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.mapper = mapper;
            this.reader = reader;
        }

        public int Priority => 200;

        public bool AnyInstancesConfigured()
        {
            return startUpInstanceRequest is StartUpDynamicInstanceRequest && mapper.ConfigState == ConfigState.Complete;
        }

        public IList<ApplicationInstanceRecord> ListInstances()
        {
            if (!AnyInstancesConfigured())
                return Enumerable.Empty<ApplicationInstanceRecord>().ToList();
            return new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("Environmental", true)
            };
        }

        public LoadedApplicationInstance LoadedApplicationInstance(ApplicationInstanceRecord applicationInstance)
        {
            return new LoadedApplicationInstance(applicationInstance.InstanceName, new EnvironmentBasedKeyValueStore(mapper, reader));
        }
    }
}