using System;
using Autofac.Core;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Diagnostics.KnowledgeBase;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    public class ConsoleHost : ICommandHost, ICommandRuntime
    {
        readonly string displayName;

        public ConsoleHost(string displayName)
        {
            this.displayName = displayName;
        }

        public void Run(Action<ICommandRuntime> start, Action shutdown)
        {
            try
            {
                Console.ResetColor();
                Console.Title = displayName;

                start(this);

                Console.ResetColor();
                shutdown();
                Console.ResetColor();
            }
            catch (DependencyResolutionException rex) when (rex.InnerException is ControlledFailureException)
            {
                throw rex.InnerException;
            }
        }

        public void OnExit(int exitCode)
        {
            if (exitCode == 0) return;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(new string('-', 79));
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("Full error details are available in the log files at ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(OctopusLogsDirectoryRenderer.LogsDirectory);
            Console.ResetColor();
            Console.Write("If you need help, please send these log files to ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("https://octopus.com/support");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(new string('-', 79));
            Console.ResetColor();
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