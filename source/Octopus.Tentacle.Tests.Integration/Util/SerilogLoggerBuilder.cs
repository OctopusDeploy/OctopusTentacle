using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
using Octopus.Tentacle.Util;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public class SerilogLoggerBuilder
    {
        public static readonly ConcurrentDictionary<string, Stopwatch> TestTimers = new();
        static readonly ILogger Logger;
        static readonly ConcurrentDictionary<string, TraceLogFileLogger> TraceLoggers = new();
        static readonly ConcurrentBag<string> HasLoggedTestHash = new();

        TraceLogFileLogger? traceFileLogger;

        static SerilogLoggerBuilder()
        {
            const string teamCityOutputTemplate =
                "{TestHash} "
                + "{Timestamp:HH:mm:ss.fff zzz} {Level:u3} "
                + "[{ShortContext}] "
                + "{Message}{NewLine}{Exception}";

            const string localOutputTemplate =
                "{Timestamp:HH:mm:ss.fff zzz} {Level:u3} "
                + "[{ShortContext}] "
                + "{Message}{NewLine}{Exception}";

            var nUnitOutputTemplate = TeamCityDetection.IsRunningInTeamCity()
                ? teamCityOutputTemplate
                : localOutputTemplate;

            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(new NonProgressNUnitSink(new MessageTemplateTextFormatter(nUnitOutputTemplate)), LogEventLevel.Information)
                .WriteTo.Sink(new TraceLogsForFailedTestsSink(new MessageTemplateTextFormatter(localOutputTemplate)), LogEventLevel.Debug)
                .CreateLogger();
        }

        public SerilogLoggerBuilder SetTraceLogFileLogger(TraceLogFileLogger logger)
        {
            this.traceFileLogger = logger;
            return this;
        }
        
        public ILogger Build()
        {
            // In teamcity we need to know what test the log is for, since we can find hung builds and only have a single file containing all log messages.
            var testName = TestContext.CurrentContext.Test.FullName;
            var testHash = CurrentTestHash();
            var logger = Logger.ForContext("TestHash", testHash);

            TestTimers.GetOrAdd(TestContext.CurrentContext.Test.ID, k => Stopwatch.StartNew());

            if (!HasLoggedTestHash.Contains(testName))
            {
                HasLoggedTestHash.Add(testName);
                logger.ForContext<SerilogLoggerBuilder>().Information($"{TestContext.CurrentContext.Test.Name} has hash {testHash}");
            }

            if (traceFileLogger != null)
            {
                TraceLoggers.AddOrUpdate(testName, traceFileLogger, (_, _) => throw new Exception("This should never be updated. If it is, it means that a test is being run multiple times in a single test run"));
            }

            return logger;
        }

        public static string CurrentTestHash()
        {
            using (SHA256 mySHA256 = SHA256.Create())
            {
                return Convert.ToBase64String(mySHA256.ComputeHash(TestContext.CurrentContext.Test.FullName.GetUTF8Bytes()))
                    .Replace("=", "")
                    .Replace("+", "")
                    .Replace("/", "")
                    .Substring(0, 10); // 64 ^ 10 is a big number, most likely we wont have collisions.
            }
        }

        /// <summary>
        /// TestContext.Progress doesn't seem to work in visual studio and sometimes in linux rider.
        /// To ensure we actually do get logs, this sink writes the logs to TestContext.Write instead.
        /// This means we don't see logs while the test is running but if the test finishes (even in failure)
        /// we do see logs.
        /// </summary>
        public class NonProgressNUnitSink : ILogEventSink
        {
            private readonly MessageTemplateTextFormatter formatter;

            public NonProgressNUnitSink(MessageTemplateTextFormatter formatter)
            {
                this.formatter = formatter != null ? formatter : throw new ArgumentNullException(nameof(formatter));
            }
            
            static Lazy<bool> IsForcingContextWrite = new(() => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Force_Test_Context_Write")));

            public void Emit(LogEvent logEvent)
            {
                if (logEvent == null)
                    throw new ArgumentNullException(nameof(logEvent));

                if (TestContext.Out == null)
                    return;
                
                var output = new StringWriter();
                if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
                {
                    var context = sourceContext.ToString().Substring(sourceContext.ToString().LastIndexOf('.') + 1).Replace("\"", "");
                    logEvent.AddOrUpdateProperty(new LogEventProperty("ShortContext", new ScalarValue(context)));
                }

                formatter.Format(logEvent, output);
                // This is the change, call this instead of: TestContext.Progress
                var elapsed = TestTimers[TestContext.CurrentContext.Test.ID].Elapsed.ToString();
                var s = elapsed + " " + output;

                if (TeamCityDetection.IsRunningInTeamCity() || IsForcingContextWrite.Value)
                {
                    TestContext.Write(s);
                }
                else
                {
                    TestContext.Progress.Write(s);
                }
            }
        }

        public class TraceLogsForFailedTestsSink : ILogEventSink
        {
            readonly MessageTemplateTextFormatter formatter;

            public TraceLogsForFailedTestsSink(MessageTemplateTextFormatter formatter) => this.formatter = formatter;

            public void Emit(LogEvent logEvent)
            {
                if (logEvent == null)
                    throw new ArgumentNullException(nameof(logEvent));

                var testName = TestContext.CurrentContext.Test.FullName;

                if (!TraceLoggers.TryGetValue(testName, out var traceLogger))
                    return;

                var output = new StringWriter();
                if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
                {
                    var context = sourceContext.ToString().Substring(sourceContext.ToString().LastIndexOf('.') + 1).Replace("\"", "");
                    logEvent.AddOrUpdateProperty(new LogEventProperty("ShortContext", new ScalarValue(context)));
                }

                formatter.Format(logEvent, output);

                var logLine = output.ToString().Trim();
                traceLogger.WriteLine(logLine);
            }
        }
    }
}
