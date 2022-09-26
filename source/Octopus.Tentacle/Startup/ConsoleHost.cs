using System;
using System.Linq;
using System.Text;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Internals.Options;

namespace Octopus.Tentacle.Startup
{
    public class ConsoleHost : ICommandHost, ICommandRuntime
    {
        private static readonly int[] FriendlyExitCodes =
        {
            (int)OctopusProgram.ExitCode.Success,
            (int)OctopusProgram.ExitCode.UnknownCommand,
            (int)OctopusProgram.ExitCode.ControlledFailureException
        };

        public static readonly string ConsoleSwitchPrototype = "console";
        public static readonly string ConsoleSwitchExample = $"--{ConsoleSwitchPrototype}";
        private readonly ISystemLog log = new SystemLog();
        private readonly string displayName;

        public ConsoleHost(string displayName)
        {
            this.displayName = displayName;
        }

        public void Run(Action<ICommandRuntime> start, Action shutdown)
        {
            Console.ResetColor();
            SafelySetConsoleTitle(displayName);
            start(this);
            Stop(shutdown);
        }

        public void Stop(Action shutdown)
        {
            Console.ResetColor();
            shutdown();
            Console.ResetColor();
        }

        public void OnExit(int exitCode)
        {
            if (FriendlyExitCodes.Contains(exitCode)) return;

            var sb = new StringBuilder()
                .AppendLine(new string('-', 79))
                .AppendLine($"Terminating process with exit code {exitCode}")
                .AppendLine("Full error details are available in the log files at:");
            foreach (var logDirectory in OctopusLogsDirectoryRenderer.LogsDirectoryHistory)
                sb.AppendLine(logDirectory);
            sb.AppendLine("If you need help, please send these log files to https://octopus.com/support");
            sb.AppendLine(new string('-', 79));
            log.Fatal(sb.ToString());
        }

        public void WaitForUserToExit()
        {
            SafelySetConsoleTitle(displayName + " - Running");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Running. Press <enter> to shut down...");
            Console.ResetColor();

            while (true)
            {
                var line = (Console.ReadLine() ?? string.Empty).ToLowerInvariant();
                if (line == "cls" || line == "clear")
                    Console.Clear();
                if (string.IsNullOrWhiteSpace(line))
                    break;
            }

            Console.ResetColor();
            SafelySetConsoleTitle(displayName + " - Shutting down...");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
        }

        /// <summary>
        /// Use this method to reliably add the --console switch to commands that want to provide an option to run as a service, or interactively.
        /// </summary>
        /// <param name="options">The OptionSet to update, adding the --console switch.</param>
        /// <param name="action">[Optional] The custom action to perform when the switch is provided.</param>
        /// <returns>The resulting OptionSet after adding the --console switch. Can be used as a convenience for method chaining.</returns>
        public static OptionSet AddConsoleSwitch(OptionSet options, Action<string>? action = null)
        {
            return options.Add(ConsoleSwitchPrototype,
                "Don't attempt to run as a service, even if the user is non-interactive",
                action ?? (v =>
                {
                    // There's actually nothing to do here. The CommandHost should have already been determined at startup.
                    // This option is added to show help and provide a hook for determining the appropriate CommandHost.
                }));
        }

        /// <summary>
        /// Use this method to reliably detect if this OptionsSet supports the --console switch.
        /// </summary>
        /// <param name="options">The OptionSet to inspect.</param>
        /// <returns>true if the OptionSet supports the --console switch; false otherwise.</returns>
        public static bool HasConsoleSwitch(OptionSet options)
        {
            return options.Any(o => o.Prototype == ConsoleSwitchPrototype);
        }

        private static void SafelySetConsoleTitle(string title)
        {
            if (Environment.UserInteractive)
                Console.Title = title;
        }
    }
}