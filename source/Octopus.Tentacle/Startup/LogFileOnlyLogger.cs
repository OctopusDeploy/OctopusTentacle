using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NLog;
using NLog.Targets;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Startup
{
    public interface ILogFileOnlyLogger
    {
        void Info(string message);
        void Warn(string message);
        void Fatal(string message);
        void AddSensitiveValues(string[] values);
    }

    /// <summary>
    /// This logger has a special rule in the nlog config file to write directly to the log file, and prevent logging to the Console.
    /// </summary>
    public class LogFileOnlyLogger : ILogFileOnlyLogger
    {
        static readonly string LoggerName = nameof(LogFileOnlyLogger);
        static readonly ILogger Log = LogManager.GetLogger(LoggerName);
        static readonly string EntryExecutable;

        static readonly string HelpMessage = $"The {EntryExecutable}.nlog file should have a rule matching the name {LoggerName} where log messages are restricted to the log file, never written to stdout or stderr.";

        static LogFileOnlyLogger()
        {
            var fullProcessPath = Assembly.GetEntryAssembly()?.FullProcessPath()!;
            EntryExecutable = PlatformDetection.IsRunningOnWindows
                ? Path.GetFileName(fullProcessPath)
                : $"{Path.GetFileName(fullProcessPath)}.exe";
        }

        public SensitiveValueMasker Masker { get; set; } = new SensitiveValueMasker();
        public static ILogFileOnlyLogger Current { get; } = new LogFileOnlyLogger();

        public static void AssertConfigurationIsCorrect()
        {
            var rule = LogManager.Configuration.LoggingRules.SingleOrDefault(r => r.LoggerNamePattern == LoggerName);
            if (rule == null)
                throw new Exception($"It looks like the {LoggerName} logging rule is not configured. {HelpMessage}");

            if (rule.Targets.Count != 1)
                throw new Exception($"The {LoggerName} rule should only have a single target. {HelpMessage}");

            var fileTarget = rule.Targets.Single() as FileTarget;
            if (fileTarget == null)
                throw new Exception($"The {LoggerName} rule should write to a file target. {HelpMessage}");
        }

        public void Info(string message) => Masker.SafeSanitize(message, s => Log.Info(s));
        public void Warn(string message) => Masker.SafeSanitize(message, s => Log.Warn(s));
        public void Fatal(string message) => Masker.SafeSanitize(message, s => Log.Fatal(s));

        public void AddSensitiveValues(string[] values)
        {
            Masker = Masker.WithSensitiveValues(values);
        }
    }
}
