using System;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Startup
{
    public class ConsoleHost : ICommandHost, ICommandRuntime
    {
        readonly ILog log = Log.Octopus();
        readonly string displayName;

        public ConsoleHost(string displayName)
        {
            this.displayName = displayName;
        }

        public void Run(Action<ICommandRuntime> start, Action shutdown)
        {
            try
            {
                Console.Title = displayName;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(new string('-', 79));
                Console.WriteLine("- " + displayName);
                Console.WriteLine(new string('-', 79));
                Console.WriteLine();
                Console.ResetColor();

                start(this);

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
                throw;
            }
        }

        public void WaitForUserToExit()
        {
            Console.Title = displayName + " - Running";

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
            Console.Title = displayName + " - Shutting down...";
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
        }
    }
}