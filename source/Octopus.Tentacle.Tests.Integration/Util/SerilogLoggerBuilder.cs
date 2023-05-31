using System;
using System.IO;
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
        public ILogger Build()
        {
            // In teamcity we need to know what test the log is for, since we can find hung builds and only have a single file containing all log messages.
            var testName = "";
            if (TentacleExeFinder.IsRunningInTeamCity())
            {
                testName = "[{TestName}] ";
            }

            var outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] [{SourceContext}] "
                + testName
                + "{Message}{NewLine}{Exception}";

            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(new NonProgressNUnitSink(new MessageTemplateTextFormatter(outputTemplate)))
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

            public NonProgressNUnitSink(MessageTemplateTextFormatter formatter) => _formatter = formatter != null ? formatter : throw new ArgumentNullException(nameof(formatter));

            public void Emit(LogEvent logEvent)
            {
            }

            public void Emit(Serilog.Events.LogEvent logEvent)
            {
                if (logEvent == null)
                    throw new ArgumentNullException(nameof(logEvent));
                if (TestContext.Out == null)
                    return;
                StringWriter output = new StringWriter();
                _formatter.Format(logEvent, output);
                // This is the change, call this instead of: TestContext.Progress
                TestContext.Write(output.ToString());
            }
        }
    }
}