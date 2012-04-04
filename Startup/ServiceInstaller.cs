using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Win32;

namespace Octopus.Shared.Startup
{
    /// <summary>
    /// A helper class for installing this application as a windows service.
    /// Stops the service if it is already installed, installs it if it is not, reconfigures it, and starts it.
    /// </summary>
    /// <remarks>
    /// Portions of this thanks to:
    /// - http://pinvoke.net/default.aspx/advapi32.CreateService
    /// - http://dl.dropbox.com/u/152585/ServiceInstaller.cs
    /// - http://haacked.com/articles/1549.aspx
    /// - http://stackoverflow.com/questions/7381508/determining-whether-a-service-is-already-installed
    /// </remarks>
    public class ServiceInstaller : IServiceInstaller
    {
        public void Install(ServiceOptions options)
        {
            AdminRequired(() => InstallAndStart(options));
        }

        public void Uninstall(string serviceName)
        {
            AdminRequired(() => UninstallService(serviceName));
        }

        void InstallAndStart(ServiceOptions options)
        {
            var serviceName = options.ServiceName;
            var assemblyContainingInstaller = options.Assembly;

            Console.WriteLine("Installing the service: " + serviceName);

            if (ServiceIsInstalled(serviceName))
            {
                Console.WriteLine("Service is already installed, it will be stopped and reconfigured");

                StopService(serviceName);
                Reconfigure(assemblyContainingInstaller, serviceName);
                StartService(serviceName);

                Console.WriteLine("Service reconfigured and restarted successfully");
            }
            else
            {
                InstallAndStart(serviceName, serviceName, assemblyContainingInstaller.FullLocalPath());
                AddServiceDescriptionToRegistry(serviceName, options.Description);
                Console.WriteLine("Service installed and started successfully");       
            }
        }

        static void Reconfigure(Assembly assemblyContainingInstaller, string serviceName)
        {
            using (var startController = new ServiceController(serviceName))
            {
                ChangeServiceConfig(
                    startController.ServiceHandle,
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

        const uint ServiceNoChange = 0xffffffff;

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

        [Flags]
        public enum ServiceManagerRights
        {
            Connect = 0x0001,

            /// <summary>
            /// 
            /// </summary>
            CreateService = 0x0002,

            /// <summary>
            /// 
            /// </summary>
            EnumerateService = 0x0004,

            /// <summary>
            /// 
            /// </summary>
            Lock = 0x0008,

            /// <summary>
            /// 
            /// </summary>
            QueryLockStatus = 0x0010,

            /// <summary>
            /// 
            /// </summary>
            ModifyBootConfig = 0x0020,

            /// <summary>
            /// 
            /// </summary>
            StandardRightsRequired = 0xF0000,

            /// <summary>
            /// 
            /// </summary>
            AllAccess = (StandardRightsRequired | Connect | CreateService |
                         EnumerateService | Lock | QueryLockStatus | ModifyBootConfig)
        }

        /// <summary>
        /// 
        /// </summary>
        [Flags]
        public enum ServiceRights
        {
            /// <summary>
            /// 
            /// </summary>
            QueryConfig = 0x1,

            /// <summary>
            /// 
            /// </summary>
            ChangeConfig = 0x2,

            /// <summary>
            /// 
            /// </summary>
            QueryStatus = 0x4,

            /// <summary>
            /// 
            /// </summary>
            EnumerateDependants = 0x8,

            /// <summary>
            /// 
            /// </summary>
            Start = 0x10,

            /// <summary>
            /// 
            /// </summary>
            Stop = 0x20,

            /// <summary>
            /// 
            /// </summary>
            PauseContinue = 0x40,

            /// <summary>
            /// 
            /// </summary>
            Interrogate = 0x80,

            /// <summary>
            /// 
            /// </summary>
            UserDefinedControl = 0x100,

            /// <summary>
            /// 
            /// </summary>
            Delete = 0x00010000,

            /// <summary>
            /// 
            /// </summary>
            StandardRightsRequired = 0xF0000,

            /// <summary>
            /// 
            /// </summary>
            AllAccess = (StandardRightsRequired | QueryConfig | ChangeConfig |
                         QueryStatus | EnumerateDependants | Start | Stop | PauseContinue |
                         Interrogate | UserDefinedControl)
        }

        /// <summary>
        /// 
        /// </summary>
        public enum ServiceBootFlag
        {
            /// <summary>
            /// 
            /// </summary>
            Start = 0x00000000,

            /// <summary>
            /// 
            /// </summary>
            SystemStart = 0x00000001,

            /// <summary>
            /// 
            /// </summary>
            AutoStart = 0x00000002,

            /// <summary>
            /// 
            /// </summary>
            DemandStart = 0x00000003,

            /// <summary>
            /// 
            /// </summary>
            Disabled = 0x00000004
        }

        /// <summary>
        /// 
        /// </summary>
        public enum ServiceState
        {
            /// <summary>
            /// 
            /// </summary>
            Unknown = -1, // The state cannot be (has not been) retrieved.
            /// <summary>
            /// 
            /// </summary>
            NotFound = 0, // The service is not known on the host server.
            /// <summary>
            /// 
            /// </summary>
            Stop = 1, // The service is NET stopped.
            /// <summary>
            /// 
            /// </summary>
            Run = 2, // The service is NET started.
            /// <summary>
            /// 
            /// </summary>
            Stopping = 3,

            /// <summary>
            /// 
            /// </summary>
            Starting = 4,
        }

        /// <summary>
        /// 
        /// </summary>
        public enum ServiceControl
        {
            /// <summary>
            /// 
            /// </summary>
            Stop = 0x00000001,

            /// <summary>
            /// 
            /// </summary>
            Pause = 0x00000002,

            /// <summary>
            /// 
            /// </summary>
            Continue = 0x00000003,

            /// <summary>
            /// 
            /// </summary>
            Interrogate = 0x00000004,

            /// <summary>
            /// 
            /// </summary>
            Shutdown = 0x00000005,

            /// <summary>
            /// 
            /// </summary>
            ParamChange = 0x00000006,

            /// <summary>
            /// 
            /// </summary>
            NetBindAdd = 0x00000007,

            /// <summary>
            /// 
            /// </summary>
            NetBindRemove = 0x00000008,

            /// <summary>
            /// 
            /// </summary>
            NetBindEnable = 0x00000009,

            /// <summary>
            /// 
            /// </summary>
            NetBindDisable = 0x0000000A
        }

        /// <summary>
        /// 
        /// </summary>
        public enum ServiceError
        {
            /// <summary>
            /// 
            /// </summary>
            Ignore = 0x00000000,

            /// <summary>
            /// 
            /// </summary>
            Normal = 0x00000001,

            /// <summary>
            /// 
            /// </summary>
            Severe = 0x00000002,

            /// <summary>
            /// 
            /// </summary>
            Critical = 0x00000003
        }

        const int STANDARD_RIGHTS_REQUIRED = 0xF0000;
        const int SERVICE_WIN32_OWN_PROCESS = 0x00000010;

        [StructLayout(LayoutKind.Sequential)]
        class SERVICE_STATUS
        {
            public int dwServiceType;
            public ServiceState dwCurrentState = 0;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        }

        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerA")]
        static extern IntPtr OpenSCManager(string lpMachineName, string
                                                                     lpDatabaseName, ServiceManagerRights dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "OpenServiceA",
            CharSet = CharSet.Ansi)]
        static extern IntPtr OpenService(IntPtr hSCManager, string
                                                                lpServiceName, ServiceRights dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "CreateServiceA")]
        static extern IntPtr CreateService(IntPtr hSCManager, string
                                                                  lpServiceName, string lpDisplayName, ServiceRights dwDesiredAccess, int
                                                                                                                                          dwServiceType, ServiceBootFlag dwStartType, ServiceError dwErrorControl,
                                           string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId, string
                                                                                                                   lpDependencies, string lp, string lpPassword);

        [DllImport("advapi32.dll")]
        static extern int CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll")]
        static extern int QueryServiceStatus(IntPtr hService,
                                             SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern int DeleteService(IntPtr hService);

        [DllImport("advapi32.dll")]
        static extern int ControlService(IntPtr hService, ServiceControl
                                                              dwControl, SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32.dll", EntryPoint = "StartServiceA")]
        static extern int StartService(IntPtr hService, int
                                                            dwNumServiceArgs, int lpServiceArgVectors);

        /// <summary>
        /// Takes a service name and tries to stop and then uninstall the windows serviceError
        /// </summary>
        /// <param name="serviceName">The windows service name to uninstall</param>
        public static void UninstallService(string serviceName)
        {
            Console.WriteLine("Uninstalling the service: " + serviceName);
            var scman = OpenScManager(ServiceManagerRights.Connect);
            try
            {
                var service = OpenService(scman, serviceName,
                                          ServiceRights.StandardRightsRequired | ServiceRights.Stop |
                                          ServiceRights.QueryStatus);
                if (service == IntPtr.Zero)
                {
                    Console.WriteLine("The service is not installed");
                    return;
                }
                try
                {
                    StopService(service);
                    var ret = DeleteService(service);
                    if (ret == 0)
                    {
                        var error = Marshal.GetLastWin32Error();
                        throw new ApplicationException("Could not delete service " + error);
                    }

                    Console.WriteLine("Uninstall complete");
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Accepts a service name and returns true if the service with that service name exists
        /// </summary>
        /// <param name="serviceName">The service name that we will check for existence</param>
        /// <returns>True if that service exists false otherwise</returns>
        public static bool ServiceIsInstalled(string serviceName)
        {
            var scman = OpenScManager(ServiceManagerRights.Connect);
            try
            {
                var service = OpenService(scman, serviceName, ServiceRights.QueryStatus);
                if (service == IntPtr.Zero) return false;
                CloseServiceHandle(service);
                return true;
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Takes a service name, a service display name and the path to the service executable and installs / starts the windows service.
        /// </summary>
        /// <param name="serviceName">The service name that this service will have</param>
        /// <param name="displayName">The display name that this service will have</param>
        /// <param name="fileName">The path to the executable of the service</param>
        static void InstallAndStart(string serviceName, string displayName, string fileName)
        {
            var scman = OpenScManager(ServiceManagerRights.Connect | ServiceManagerRights.CreateService);
            try
            {
                var service = OpenService(scman, serviceName, ServiceRights.QueryStatus | ServiceRights.Start);
                if (service == IntPtr.Zero)
                {
                    service = CreateService(scman, serviceName, serviceName,
                        ServiceRights.QueryStatus | ServiceRights.Start, SERVICE_WIN32_OWN_PROCESS,
                        ServiceBootFlag.AutoStart, ServiceError.Normal, fileName, null, IntPtr.Zero,
                        null, null, null);
                }
                if (service == IntPtr.Zero)
                {
                    throw new ApplicationException("Failed to install service.");
                }
                try
                {
                    StartService(service);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Takes a service name and starts it
        /// </summary>
        /// <param name="name">The service name</param>
        static void StartService(string name)
        {
            var scman = OpenScManager(ServiceManagerRights.Connect);
            try
            {
                var hService = OpenService(scman, name, ServiceRights.QueryStatus | ServiceRights.Start);
                if (hService == IntPtr.Zero)
                {
                    throw new ApplicationException("Could not open service.");
                }
                try
                {
                    StartService(hService);
                }
                finally
                {
                    CloseServiceHandle(hService);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Stops the provided windows service
        /// </summary>
        /// <param name="name">The service name that will be stopped</param>
        static void StopService(string name)
        {
            var scman = OpenScManager(ServiceManagerRights.Connect);
            try
            {
                var hService = OpenService(scman, name, ServiceRights.QueryStatus |
                                                        ServiceRights.Stop);
                if (hService == IntPtr.Zero)
                {
                    throw new ApplicationException("Could not open service.");
                }
                try
                {
                    StopService(hService);
                }
                finally
                {
                    CloseServiceHandle(hService);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Stars the provided windows service
        /// </summary>
        /// <param name="hService">The handle to the windows service</param>
        static void StartService(IntPtr hService)
        {
            StartService(hService, 0, 0);
            WaitForServiceStatus(hService, ServiceState.Starting, ServiceState.Run);
        }

        /// <summary>
        /// Stops the provided windows service
        /// </summary>
        /// <param name="hService">The handle to the windows service</param>
        static void StopService(IntPtr hService)
        {
            var status = new SERVICE_STATUS();
            ControlService(hService, ServiceControl.Stop, status);
            WaitForServiceStatus(hService, ServiceState.Stopping, ServiceState.Stop);
        }

        /// <summary>
        /// Takes a service name and returns the <code>ServiceState</code> of the corresponding service
        /// </summary>
        /// <param name="serviceName">The service name that we will check for his <code>ServiceState</code></param>
        /// <returns>The ServiceState of the service we wanted to check</returns>
        public static ServiceState GetServiceStatus(string serviceName)
        {
            var scman = OpenScManager(ServiceManagerRights.Connect);
            try
            {
                var hService = OpenService(scman, serviceName, ServiceRights.QueryStatus);
                if (hService == IntPtr.Zero)
                {
                    return ServiceState.NotFound;
                }
                try
                {
                    return GetServiceStatus(hService);
                }
                finally
                {
                    CloseServiceHandle(scman);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Gets the service state by using the handle of the provided windows service
        /// </summary>
        /// <param name="hService">The handle to the service</param>
        /// <returns>The <code>ServiceState</code> of the service</returns>
        static ServiceState GetServiceStatus(IntPtr hService)
        {
            var ssStatus = new SERVICE_STATUS();
            if (QueryServiceStatus(hService, ssStatus) == 0)
            {
                throw new ApplicationException("Failed to query service status.");
            }
            return ssStatus.dwCurrentState;
        }

        /// <summary>
        /// Returns true when the service status has been changes from wait status to desired status
        /// ,this method waits around 10 seconds for this operation.
        /// </summary>
        /// <param name="hService">The handle to the service</param>
        /// <param name="waitStatus">The current state of the service</param>
        /// <param name="desiredStatus">The desired state of the service</param>
        /// <returns>bool if the service has successfully changed states within the allowed timeline</returns>
        static void WaitForServiceStatus(IntPtr hService, ServiceState waitStatus, ServiceState desiredStatus)
        {
            var ssStatus = new SERVICE_STATUS();
            int dwOldCheckPoint;
            int dwStartTickCount;

            QueryServiceStatus(hService, ssStatus);
            if (ssStatus.dwCurrentState == desiredStatus) return;
            dwStartTickCount = Environment.TickCount;
            dwOldCheckPoint = ssStatus.dwCheckPoint;

            while (ssStatus.dwCurrentState == waitStatus)
            {
                // Do not wait longer than the wait hint. A good interval is
                // one tenth the wait hint, but no less than 1 second and no
                // more than 10 seconds.

                var dwWaitTime = ssStatus.dwWaitHint/10;

                if (dwWaitTime < 1000) dwWaitTime = 1000;
                else if (dwWaitTime > 10000) dwWaitTime = 10000;

                Thread.Sleep(dwWaitTime);

                // Check the status again.

                if (QueryServiceStatus(hService, ssStatus) == 0) break;

                if (ssStatus.dwCheckPoint > dwOldCheckPoint)
                {
                    // The service is making progress.
                    dwStartTickCount = Environment.TickCount;
                    dwOldCheckPoint = ssStatus.dwCheckPoint;
                }
                else
                {
                    if (Environment.TickCount - dwStartTickCount > ssStatus.dwWaitHint)
                    {
                        // No progress made within the wait hint
                        break;
                    }
                }
            }
            return;
        }

        /// <summary>
        /// Opens the service manager
        /// </summary>
        /// <param name="Rights">The service manager rights</param>
        /// <returns>the handle to the service manager</returns>
        static IntPtr OpenScManager(ServiceManagerRights Rights)
        {
            var scman = OpenSCManager(null, null, Rights);
            if (scman == IntPtr.Zero)
            {
                throw new ApplicationException("Could not connect to service control manager.");
            }
            return scman;
        }

        /// <summary>
        /// Adds the service description to the registry.
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="description"></param>
        protected virtual void AddServiceDescriptionToRegistry(string serviceName, string description)
        {
            var system = Registry.LocalMachine.OpenSubKey("System");
            if (system == null) return;
            var currentControlSet = system.OpenSubKey("CurrentControlSet");
            if (currentControlSet != null)
            {
                var services = currentControlSet.OpenSubKey("Services");
                if (services != null)
                {
                    var service = services.OpenSubKey(serviceName, true);
                    if (service != null)
                    {
                        service.SetValue("Description", description);
                        var parameters = service.CreateSubKey("Parameters");
                        if (parameters != null) parameters.Close();
                        service.Close();
                    }
                    services.Close();
                }
                currentControlSet.Close();
            }
            system.Close();
        }
    }
}