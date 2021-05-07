using System;

namespace Octopus.Shared.Startup
{
    public interface IServiceConfigurator
    {
        void ConfigureService(string thisServiceName,
            string exePath,
            string workingDir,
            string? instance,
            string serviceDescription,
            ServiceConfigurationState serviceConfigurationState);
    }
}