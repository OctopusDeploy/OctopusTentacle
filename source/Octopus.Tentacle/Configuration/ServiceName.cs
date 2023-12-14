using System;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Tentacle.Configuration
{
    public static class ServiceName
    {
        public static string GetWindowsServiceName(ApplicationName application, string? instanceName)
        {
            string? name;

            switch (application)
            {
                case ApplicationName.Tentacle:
                    name = "OctopusDeploy Tentacle";
                    break;
                default:
                    throw new ArgumentException("Invalid application name", nameof(application));
            }

            var defaultInstanceName = ApplicationInstanceRecord.GetDefaultInstance(application);
            if (instanceName == null || defaultInstanceName == instanceName)
                return name;

            return name + ": " + instanceName;
        }
    }
}