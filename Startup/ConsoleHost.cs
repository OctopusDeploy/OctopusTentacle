using System;
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
            catch (ControlledFailureException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(new string('-', 79));
                Console.WriteLine("Error: " + ex.GetErrorSummary());
                Console.WriteLine(new string('-', 79));
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("Full error details are available in the log files.");
                Console.Write("At: ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(OctopusLogsDirectoryRenderer.LogsDirectory);
                Console.ResetColor();

                ExceptionKnowledgeBaseEntry entry;
                if (ExceptionKnowledgeBase.TryInterpret(ex, out entry))
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(new string('=', 79));
                    Console.WriteLine(entry.Summary);
                    if (entry.HelpText != null || entry.HelpLink != null)
                    {
                        Console.WriteLine(new string('-', 79));
                        if (entry.HelpText != null)
                        {
                            Console.WriteLine(entry.HelpText);
                        }
                        if (entry.HelpLink != null)
                        {
                            Console.Write("See: ");
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine(entry.HelpLink);
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                    }
                    Console.WriteLine(new string('=', 79));
                    Console.ResetColor();
                }

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