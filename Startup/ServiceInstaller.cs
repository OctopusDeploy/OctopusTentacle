using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace Octopus.Shared.Startup
{
    /// <summary>
    /// A helper class for installing this application as a windows service.
    /// Stops the service if it is already installed, installs it if it is not, reconfigures it, and starts it.
    /// </summary>
    public class ServiceInstaller
    {
        readonly string serviceName;
        readonly Assembly assemblyContainingInstaller;

        private ServiceInstaller(string serviceName, Assembly assemblyContainingInstaller)
        {
            this.serviceName = serviceName;
            this.assemblyContainingInstaller = assemblyContainingInstaller;
        }

        public static void Install(string name, Assembly assemblyContainingInstaller)
        {
            var installer = new ServiceInstaller(name, assemblyContainingInstaller);
            AdminRequired(installer.InstallAndStart);
        }

        void InstallAndStart()
        {
            if (ServiceIsInstalled())
            {
                Console.WriteLine("Service is already installed");

                StopService();

                using (var startController = new ServiceController(serviceName))
                {
                    ChangeServiceConfig(startController.ServiceHandle,
                        ServiceNoChange,
                        ServiceNoChange,
                        ServiceNoChange,
                        assemblyContainingInstaller.FullLocalPath(),
                        null,
                        IntPtr.Zero,
                        null,
                        null,
                        null,
                        null);

                    Thread.Sleep(100);

                    startController.Start();
                }
                return;
            }

            ManagedInstallerClass.InstallHelper(new[] { assemblyContainingInstaller.FullLocalPath() });

            Thread.Sleep(1000);

            using (var startController = new ServiceController(serviceName))
            {
                startController.Start();
            }

            Console.WriteLine("Service installed successfully");
        }

        void StopService()
        {
            var stopController = new ServiceController(serviceName);

            if (stopController.Status != ServiceControllerStatus.Running)
                return;

            stopController.Stop();
            stopController.WaitForStatus(ServiceControllerStatus.Stopped);
            Console.WriteLine("Service stopped");
        }

        bool ServiceIsInstalled()
        {
            return (ServiceController.GetServices().Count(s => s.ServiceName == serviceName) > 0);
        }

        static void AdminRequired(Action actionThatMayRequireAdminPrivileges)
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            if (principal.IsInRole(WindowsBuiltInRole.Administrator) == false)
            {
                Console.WriteLine("This command must be executed by a member of the administrators role, with elevated privelliges.");
                return;
            }
            actionThatMayRequireAdminPrivileges();
        }

        private const uint ServiceNoChange = 0xffffffff;

        [DllImport("Advapi32.dll", EntryPoint = "ChangeServiceConfigW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        internal static extern bool ChangeServiceConfig(
            SafeHandle hService,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            [In] string lpBinaryPathName,
            [In] string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            [In] string lpDependencies,
            [In] string lpServiceStartName,
            [In] string lpPassWord,
            [In] string lpDisplayName
        );
    }
}
