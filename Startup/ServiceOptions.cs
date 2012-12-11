using System;
using System.Reflection;
using System.ServiceProcess;

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
            DefaultAccount = ServiceAccount.LocalSystem;
        }

        public string ServiceName
        {
            get { return serviceName; }
        }

        public Assembly Assembly
        {
            get { return assembly; }
        }

        public ServiceAccount DefaultAccount { get; set; }

        public string Description { get; set; }
    }
}