using System;
using System.IO;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Util;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.NUnit;
using LogEvent = Halibut.Diagnostics.LogEvent;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public class SerilogLoggerBuilder
    {
        
        
        public ILogger Build()
        {
            // Sigh progress messages don't work in linux
            // TODO why is the normal nunit sink now working on linux.
            //var sink = PlatformDetection.IsRunningOnNix ? (ILogEventSink)new NUnitLinuxSink(new MessageTemplateTextFormatter(OutputTemplate)) : new NUnitSink(new MessageTemplateTextFormatter(OutputTemplate));
            
            // TODO these are out of order locally.
            // ILogEventSink sink;
            // if (TentacleExeFinder.IsRunningInTeamCity())
            // {
            //ILogEventSink sink = new NUnitLinuxSink(new MessageTemplateTextFormatter(OutputTemplate));
            // }

            // In teamcity we need to know what test the log is for.
            var testName = "";
            if (TentacleExeFinder.IsRunningInTeamCity())
            {
                testName = "[{TestName}] ";
            }
            
            var OutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] [{SourceContext}] "
                + testName 
                + "{Message}{NewLine}{Exception}";
            
            
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(new NonProgressNUnitSink(new MessageTemplateTextFormatter(OutputTemplate)))
                .Enrich.WithProperty("TestName", TestContext.CurrentContext.Test.Name)
                .CreateLogger();
        }
        
        
        public class NonProgressNUnitSink : ILogEventSink
        {
            private readonly MessageTemplateTextFormatter _formatter;

            public NonProgressNUnitSink(MessageTemplateTextFormatter formatter) => this._formatter = formatter != null ? formatter : throw new ArgumentNullException(nameof (formatter));

            public void Emit(LogEvent logEvent)
            {
                
            }

            public void Emit(Serilog.Events.LogEvent logEvent)
            {
                if (logEvent == null)
                    throw new ArgumentNullException(nameof (logEvent));
                if (TestContext.Out == null)
                    return;
                StringWriter output = new StringWriter();
                this._formatter.Format(logEvent, (TextWriter) output);
                // This is the change, call this instead of: TestContext.Progress
                TestContext.Write(output.ToString());
                File.AppendAllText("/tmp/nunitlog", output.ToString());
            }
        }
    }
}