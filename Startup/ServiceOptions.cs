using System;
using System.Reflection;

namespace Octopus.Shared.Startup
{
    public class ServiceOptions
    {
        readonly string serviceName;
        readonly Assembly assembly;

        public ServiceOptions(string serviceName, Assembly assembly)
        {
            this.serviceName = serviceName;
            this.assembly = assembly;
        }

        public string ServiceName
        {
            get { return serviceName; }
        }

        public Assembly Assembly
        {
            get { return assembly; }
        }

        public string Description { get; set; }
    }
}