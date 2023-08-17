using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using Halibut.Diagnostics;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support;
using Serilog;
using Serilog.Core;
using Serilog.Formatting.Display;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public class SerilogLoggerBuilder
    {
        public static readonly ConcurrentDictionary<string, Stopwatch> TestTimers = new ConcurrentDictionary<string, Stopwatch>();

        StringBuilder? stringBuilder;

        public SerilogLoggerBuilder WithLoggingToStringBuilder(StringBuilder stringBuilder)
        {
            this.stringBuilder = stringBuilder;
            return this;
        }
        
        public ILogger Build()
        {
            // In teamcity we need to know what test the log is for, since we can find hung builds and only have a single file containing all log messages.
            var testName = "";
            if (TentacleExeFinder.IsRunningInTeamCity())
            {
                testName = "[{TestName}] ";
            }

            TestTimers.GetOrAdd(TestContext.CurrentContext.Test.ID, k => Stopwatch.StartNew());

            var outputTemplate = 
                testName
                + "{Message}{NewLine}{Exception}";
            
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(new NonProgressNUnitSink(new MessageTemplateTextFormatter(outputTemplate), stringBuilder))
                .Enrich.WithProperty("TestName", TestContext.CurrentContext.Test.Name)
                .CreateLogger();
        }

        /// <summary>
        /// TestContext.Progress doesn't seem to work in visual studio and sometimes in linux rider.
        /// To ensure we actually do get logs, this sink writes the logs to TestContext.Write instead.
        /// This means we don't see logs while the test is running but if the test finishes (even in failure)
        /// we do see logs.
        /// </summary>
        public class NonProgressNUnitSink : ILogEventSink
        {
            private readonly MessageTemplateTextFormatter _formatter;
            StringBuilder? stringBuilder;

            public NonProgressNUnitSink(MessageTemplateTextFormatter formatter, StringBuilder? stringBuilder)
            {
                this.stringBuilder = stringBuilder;
                _formatter = formatter != null ? formatter : throw new ArgumentNullException(nameof(formatter));
            }

            public void Emit(LogEvent logEvent)
            {
            }
            
            static Lazy<bool> IsForcingContextWrite = new(() => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Force_Test_Context_Write")));

            public void Emit(Serilog.Events.LogEvent logEvent)
            {
                if (logEvent == null)
                    throw new ArgumentNullException(nameof(logEvent));
                if (TestContext.Out == null)
                    return;
                StringWriter output = new StringWriter();
                if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
                {
                    output.Write("[" + sourceContext.ToString().Substring(sourceContext.ToString().LastIndexOf('.') + 1).Replace("\"", "") + "] ");
                }
                _formatter.Format(logEvent, output);
                // This is the change, call this instead of: TestContext.Progress
                var elapsed = SerilogLoggerBuilder.TestTimers[TestContext.CurrentContext.Test.ID].Elapsed.ToString();
                var s = elapsed + " " + output.ToString();
                if (stringBuilder != null)
                {
                    lock (stringBuilder)
                    {
                        stringBuilder.Append(s);
                    }
                }
                if (TentacleExeFinder.IsRunningInTeamCity() || IsForcingContextWrite.Value)
                {
                    TestContext.Write(s);
                }
                else
                {
                    TestContext.Progress.Write(s);
                }
            }
        }
    }
}