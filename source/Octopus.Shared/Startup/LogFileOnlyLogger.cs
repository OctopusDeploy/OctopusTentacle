using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NLog;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    /// <summary>
    /// This logger has a special rule in the nlog config file to write directly to the log file, and prevent logging to the Console.
    /// </summary>
    public class LogFileOnlyLogger
    {
        static readonly string LoggerName = nameof(LogFileOnlyLogger);
        static readonly ILogger Log = LogManager.GetLogger(LoggerName);
        private static readonly string EntryExecutable = Path.ChangeExtension(Path.GetFileName(Assembly.GetEntryAssembly()?.FullLocalPath()), "exe") ?? "*";
        static readonly string HelpMessage = $"The {EntryExecutable}.nlog file should have a rule matching the name {LoggerName} where log messages are restricted to the log file, never written to stdout or stderr.";

        public static void AssertConfigurationIsCorrect()
        {
            var rule = LogManager.Configuration.LoggingRules.SingleOrDefault(r => r.LoggerNamePattern == LoggerName);
            if (rule == null)
                throw new Exception($"It looks like the {LoggerName} logging rule is not configured. {HelpMessage}");


            if (rule.Targets.Count != 1)
                throw new Exception($"The {LoggerName} rule should only have a single target. {HelpMessage}");

            var fileTarget = rule.Targets.Single() as NLog.Targets.FileTarget;
            if (fileTarget == null)
                throw new Exception($"The {LoggerName} rule should write to a file target. {HelpMessage}");
        }

        public static ILogContext Context { get; set; } = new LogContext();

        public static void Info(string message) => Context.SafeSanitize(message, s => Log.Info(s));
        public static void Warn(string message) => Context.SafeSanitize(message, s => Log.Warn(s));
        public static void Error(string message) => Context.SafeSanitize(message, s => Log.Error(s));
        public static void Error(Exception ex, string message) => Context.SafeSanitize(message, s => Log.Error(ex, s));

        public static void AddSensitiveValues(string[] values)
        {
            Context = Context.WithSensitiveValues(values);
        }
    }
}
