using System;

namespace Octopus.Shared.Configuration
{
    public class StartUpRegistryInstanceRequest : StartUpInstanceRequest
    {
        /// <summary>
        /// Used to represent that the command line was invoked with an explicit instance name
        /// </summary>
        /// <param name="applicationName">The application being executed, i.e. OctopusServer or Tentacle</param>
        /// <param name="instanceName">Non-blank instance name. If no instance name was provided on the command line then we should have a <see cref="StartUpDynamicInstanceRequest" /></param>
        public StartUpRegistryInstanceRequest(ApplicationName applicationName, string instanceName) : base(applicationName)
        {
            if (string.IsNullOrWhiteSpace(instanceName))
                throw new ControlledFailureException("StartUpDynamicInstanceRequest should be used when no instanceName is specified");
            InstanceName = instanceName;
        }

        public string InstanceName { get; }
    }
}