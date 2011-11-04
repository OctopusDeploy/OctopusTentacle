using System;
using System.IO;
using System.Reflection;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Startup
{
    /// <summary>
    /// Runs an application interactively or as a service, depending on how the 
    /// process is launched.
    /// </summary>
    public class ServiceOrConsole
    {
        public static void RunConsole(Action execute, bool waitForExit)
        {
            var name = GetName();
            
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(new string('-', 79));
                Console.WriteLine("- " + name);
                Console.WriteLine(new string('-', 79));
                Console.WriteLine();
                Console.ResetColor();

                Console.Title = name + " - Running";

                execute();

                if (waitForExit)
                {
                    Console.Title = name + " - Running";

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Running. Press <enter> to shut down...");
                    Console.ResetColor();

                    Console.ReadLine();

                    Console.Title = name + " - Shutting down...";
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine();
                }

                Console.ResetColor();
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

        public static void RunMostAppropriate(Action execute, bool waitForExit)
        {
            var name = GetName();

            if (Environment.UserInteractive)
            {
                RunConsole(execute, waitForExit);
            }
            else
            {
                RunService(execute, name);
            }
        }

        public static void RunService(Action execute, string name)
        {
            Logger.Default.Info("Starting server " + name + " Windows Service");

            try
            {
                new WindowsServiceHost(execute).Start();
            }
            catch (Exception ex)
            {
                Logger.Default.Error(ex);
            }
        }

        static string GetName()
        {
            return Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().FullLocalPath());
        }
    }
}