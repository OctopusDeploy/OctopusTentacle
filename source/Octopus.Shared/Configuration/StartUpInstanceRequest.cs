using System;

namespace Octopus.Shared.Configuration
{
    public abstract class StartUpInstanceRequest
    {
        protected StartUpInstanceRequest(ApplicationName applicationName)
        {
            ApplicationName = applicationName;
        }

        public ApplicationName ApplicationName { get; }
    }
}