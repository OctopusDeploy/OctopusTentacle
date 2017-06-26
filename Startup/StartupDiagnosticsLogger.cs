using System;
using NLog;

namespace Octopus.Shared.Startup
{
    /// <summary>
    /// This logger has a special rule in the nlog config file to write directly to the log file, and prevent logging to the Console.
    /// </summary>
    public class StartupDiagnosticsLogger
    {
        static readonly ILogger Log = LogManager.GetLogger(nameof(StartupDiagnosticsLogger));

        public static void Info(string message) => Log.Info(message);
        public static void Warn(string message) => Log.Warn(message);
    }
}