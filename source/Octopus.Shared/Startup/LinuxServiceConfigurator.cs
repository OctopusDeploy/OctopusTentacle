using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Octopus.Diagnostics;

namespace Octopus.Shared.Startup
{
    public class LinuxServiceConfigurator : IServiceConfigurator
    {
        readonly ILog log;
        readonly string thisServiceName;
        readonly string exePath;
        readonly string instance;
        readonly string serviceDescription;
        readonly ServiceConfigurationState serviceConfigurationState;

        public LinuxServiceConfigurator(ILog log, string thisServiceName, string exePath, string instance, string serviceDescription, ServiceConfigurationState serviceConfigurationState)
        {
            this.log = log;
            this.thisServiceName = thisServiceName;
            this.exePath = exePath;
            this.instance = instance;
            this.serviceDescription = serviceDescription;
            this.serviceConfigurationState = serviceConfigurationState;
        }

        public void ConfigureService()
        {
        }
    }
}
