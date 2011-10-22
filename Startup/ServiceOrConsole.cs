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
        public static void Run(Action execute, bool waitForExit)
        {
            var name = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().FullLocalPath());

            if (Environment.UserInteractive)
            {
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
            else
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
        }
    }
}