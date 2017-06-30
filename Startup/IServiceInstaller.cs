#if WINDOWS_SERVICE
using System;

namespace Octopus.Shared.Startup
{
    /// <summary>
    /// A helper class for installing this application as a windows service.
    /// Stops the service if it is already installed, installs it if it is not, reconfigures it, and starts it.
    /// </summary>
    public interface IServiceInstaller
    {
        void Install(ServiceOptions options);
        void Reconfigure(ServiceOptions options);
        void Uninstall(string serviceName);
        void Restart(string serviceName);
        string GetExecutable(string serviceName);
    }
}
#endif