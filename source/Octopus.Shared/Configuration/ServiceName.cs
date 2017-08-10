using System;

namespace Octopus.Shared.Configuration
{
    public static class ServiceName
    {
        public static string GetWindowsServiceName(ApplicationName application, string instanceName)
        {
            string name = null;

            switch (application)
            {
                case ApplicationName.OctopusServer:
                    name = "OctopusDeploy";
                    break;
                case ApplicationName.Tentacle:
                    name = "OctopusDeploy Tentacle";
                    break;
            }

            var defaultInstanceName = ApplicationInstanceRecord.GetDefaultInstance(application);
            if (defaultInstanceName == instanceName)
            {
                return name;
            }

            return name + ": " + instanceName;
        }
    }
}