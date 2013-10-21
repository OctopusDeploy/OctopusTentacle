using System;
using Octopus.Platform.Diagnostics;

namespace Octopus.Shared.Startup
{
    public class ConsoleHost : ICommandHost, ICommandRuntime
    {
        readonly ILog log = Log.Octopus();
        readonly string displayName;
        readonly bool showLogo;

        public ConsoleHost(string displayName, bool showLogo)
        {
            this.displayName = displayName;
            this.showLogo = showLogo;
        }

        public void Run(Action<ICommandRuntime> start, Action shutdown)
        {
            try
            {
                Console.ResetColor();
                Console.Title = displayName;
                if (showLogo)
                {                 
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(new string('-', 79));
                    Console.WriteLine("- " + displayName);
                    Console.WriteLine(new string('-', 79));
                    Console.WriteLine();
                    Console.ResetColor();
                }

                start(this);

                Console.ResetColor();
                shutdown();
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(new string('-', 79));
                Console.WriteLine("A fatal exception occurred");
                Console.WriteLine(ex);
                Console.WriteLine(new string('-', 79));
                Console.ResetColor();
                log.Fatal(ex);
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