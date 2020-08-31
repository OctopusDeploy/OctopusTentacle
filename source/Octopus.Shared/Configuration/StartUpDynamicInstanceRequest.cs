using System;

namespace Octopus.Shared.Configuration
{
    public class StartUpDynamicInstanceRequest : StartUpInstanceRequest
    {
        public StartUpDynamicInstanceRequest(ApplicationName applicationName) : base(applicationName)
        {
        }
    }
}