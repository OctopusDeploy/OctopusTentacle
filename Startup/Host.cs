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
    public class Host : IHost
    {
        readonly ILog log = LogAdapter.GetDefault();

        public void RunConsole(Action execute, Action shutdown)
        {
            InternalRunConsole(execute, shutdown, false);
        }

        public void RunConsoleWithPause(Action execute, Action shutdown)
        {
            InternalRunConsole(execute, shutdown, true);
        }

        public void RunServiceOrConsole(Action execute, Action shutdown)
        {
            InternalRunServiceOrConsole(execute, shutdown, false);
        }

        public void RunServiceOrConsoleWithPause(Action execute, Action shutdown)
        {
            InternalRunServiceOrConsole(execute, shutdown, true);
        }

        public void RunService(Action execute, Action shutdown)
        {
            var name = GetName();

            log.Info("Starting " + name + " Windows Service");

            try
            {
                new WindowsServiceHost(execute, LogAndShutdown(name, shutdown)).Start();
            }
            catch (Exception ex)
            {
                log.Fatal(ex);
            }
        }

        void InternalRunServiceOrConsole(Action execute, Action shutdown, bool pause)
        {
            if (Environment.UserInteractive)
            {
                InternalRunConsole(execute, shutdown, pause);
            }
            else
            {
                RunService(execute, shutdown);
            }
        }

        void InternalRunConsole(Action execute, Action shutdown, bool waitForExit)
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

                    while (true)
                    {
                        var line = (Console.ReadLine() ?? string.Empty).ToLowerInvariant();
                        if (line == "cls" || line == "clear")
                        {
                            Console.Clear();
                        }
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            break;
                        }
                    }

                    Console.ResetColor();
                    Console.Title = name + " - Shutting down...";
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine();
                }

                Console.ResetColor();

                shutdown();

                Console.ResetColor();
            }
            catch (Exception ex)
            {
                log.Fatal(ex);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Unhandled exception:");
                Console.ResetColor();
                Console.WriteLine(ex);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                Environment.Exit(-1);
            }
        }

        Action LogAndShutdown(string name, Action shutdown)
        {
            return delegate
            {
                log.Info("Stopping " + name + " Windows Service");

                try
                {
                    shutdown();
                }
                catch (Exception ex)
                {
                    log.Fatal(ex);
                    throw;
                }
            };
        }

        static string GetName()
        {
            return Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().FullLocalPath());
        }
    }
}