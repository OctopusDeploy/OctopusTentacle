using System;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Startup
{
    /// <summary>
    /// Runs an application interactively or as a service, depending on how the 
    /// process is launched.
    /// </summary>
    public class ServiceOrConsole
    {
        public static void Run(string name, Action execute)
        {
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

                    Console.Title = name + " - Starting...";

                    execute();

                    Console.Title = name + " - Running";
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Server is running");
                    Console.WriteLine();
                    Console.ResetColor();
                    Console.ReadLine();

                    Console.Title = name + " - Shutting down...";
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Server is shutting down...");
                    Console.WriteLine();
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