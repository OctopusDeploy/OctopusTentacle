using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Startup
{
    /// <summary>
    /// Runs an application interactively or as a service, depending on how the 
    /// process is launched.
    /// </summary>
    public class ApplicationRunner
    {
        readonly IApplication application;
        readonly string applicationDisplayName;
        readonly string applicationExecutableName;

        public ApplicationRunner(IApplication application)
        {
            this.application = application;
            applicationExecutableName = application.GetType().Assembly.GetName().Name + ".exe";
            applicationDisplayName = application.GetType().GetCustomAttributes(true).OfType<DisplayNameAttribute>().Single().DisplayName;
        }

        public void Run(string[] args)
        {
            if (Environment.UserInteractive)
            {
                Logger.Default.Info("Starting server " + applicationDisplayName + " in interactive mode");

                try
                {
                    RunInteractive(args);
                }
                catch (Exception ex)
                {
                    Logger.Default.Error(ex);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Unhandled exception:");
                    Console.ResetColor();
                    Console.WriteLine(ex);
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    Environment.Exit(-1);
                }
            }
            else
            {
                Logger.Default.Info("Starting server " + applicationDisplayName + " Windows Service");

                try
                {
                    new WindowsServiceHost(application).Start();
                }
                catch (Exception ex)
                {
                    Logger.Default.Error(ex);
                }
            }
        }

        void RunInteractive(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(new string('-', 79));
            Console.WriteLine("- " + applicationDisplayName);
            Console.WriteLine(new string('-', 79));
            Console.WriteLine();
            Console.ResetColor();

            var command = GetFirstArgument(args);
            switch (command)
            {
                case "":
                case "test":
                case "debug":
                    StartDebugMode();
                    break;
                case "start":
                    AdminRequired(StartService);
                    break;
                case "stop":
                    AdminRequired(StopService);
                    break;
                case "restart":
                    AdminRequired(RestartService);
                    break;
                case "install":
                    AdminRequired(InstallAndStart);
                    break;
                case "h":
                case "help":
                case "?":
                    PrintHelp();
                    break;
                default:
                    Console.WriteLine("Unknown command: " + command);
                    Console.WriteLine();
                    PrintHelp();
                    break;
            }
        }

        static string GetFirstArgument(string[] args)
        {
            return args.Length == 0
                       ? ""
                       : args[0].Replace("/", "").Replace("-", "").ToLowerInvariant();
        }

        void PrintHelp()
        {
            Console.Write("Usage: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(applicationExecutableName + " [command]");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Where command is one of:");
            Console.WriteLine();
            Console.WriteLine("  /start          Starts the windows service");
            Console.WriteLine("  /stop           Stops the windows service");
            Console.WriteLine("  /restart        Restarts the windows service");
            Console.WriteLine("  /install        Installs and starts the windows service");
            Console.WriteLine();
        }

        static void AdminRequired(Action actionThatMayRequireAdminPrivileges)
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            if (principal.IsInRole(WindowsBuiltInRole.Administrator) == false)
            {
                RunAgainAsAdmin();
                return;
            }
            actionThatMayRequireAdminPrivileges();
        }

        static void RunAgainAsAdmin()
        {
            Console.WriteLine("This command must be executed by a member of the administrators role, with elevated privelliges.");
        }

        void StartDebugMode()
        {
            Console.Title = applicationDisplayName + " - Starting...";
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("This mode is used for debugging and testing. To start the Windows Service,");
            Console.WriteLine("use this command:");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  " + applicationExecutableName + " /start");
            Console.WriteLine();
            Console.WriteLine("If the service is not installed, use the MSI to install it.");
            Console.WriteLine();
            Console.ResetColor();

            application.Run();

            Console.Title = applicationDisplayName + " - Running";
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Server is running");
            Console.WriteLine();
            Console.ResetColor();
            Console.ReadKey();

            Console.Title = applicationDisplayName + " - Shutting down...";
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Server is shutting down...");
            Console.WriteLine();
            Console.ResetColor();
        }

        void StopService()
        {
            var stopController = new ServiceController(applicationDisplayName);

            if (stopController.Status != ServiceControllerStatus.Running)
                return;

            stopController.Stop();
            stopController.WaitForStatus(ServiceControllerStatus.Stopped);
            Console.WriteLine("Service stopped");
        }

        void StartService()
        {
            var stopController = new ServiceController(applicationDisplayName);

            if (stopController.Status == ServiceControllerStatus.Running)
                return;

            stopController.Start();
            stopController.WaitForStatus(ServiceControllerStatus.Running);
            Console.WriteLine("Service started");
        }

        void RestartService()
        {
            var stopController = new ServiceController(applicationDisplayName);

            if (stopController.Status == ServiceControllerStatus.Running)
            {
                stopController.Stop();
                stopController.WaitForStatus(ServiceControllerStatus.Stopped);
            }

            if (stopController.Status == ServiceControllerStatus.Running)
                return;

            stopController.Start();
            stopController.WaitForStatus(ServiceControllerStatus.Running);
            Console.WriteLine("Service restarted");
        }

        bool ServiceIsInstalled()
        {
            return (ServiceController.GetServices().Count(s => s.ServiceName == applicationDisplayName) > 0);
        }

        void InstallAndStart()
        {
            if (ServiceIsInstalled())
            {
                Console.WriteLine("Service is already installed");
                StopService();

                ManagedInstallerClass.InstallHelper(new[] { "/u", application.GetType().Assembly.Location });

                Thread.Sleep(1000);
            }

            ManagedInstallerClass.InstallHelper(new[] {application.GetType().Assembly.Location});

            var startController = new ServiceController(applicationDisplayName);
            startController.Start();
            
            Console.WriteLine("Service installed successfully");
        }
    }
}