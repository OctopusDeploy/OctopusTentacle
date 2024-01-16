using System;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Serilog;
using Log = Octopus.Tentacle.Diagnostics.Log;

namespace Octopus.Tentacle.Client.Diagnostics
{
    public static class ILoggerExtensionMethods
    {
        public static ILog ToILog(this ILogger logger)
        {
            return new SerilogILoggerILog(logger);
        }

        class SerilogILoggerILog : Log
        {
            private ILogger logger;

            public SerilogILoggerILog(ILogger logger)
            {
                this.logger = logger;
            }

            public override bool IsEnabled(LogCategory category) => true;

            protected override void WriteEvent(LogEvent logEvent)
            {
                var logLevel = logEvent.Category switch
                {
                    LogCategory.Trace => Serilog.Events.LogEventLevel.Debug,
                    LogCategory.Verbose => Serilog.Events.LogEventLevel.Verbose,
                    LogCategory.Info => Serilog.Events.LogEventLevel.Information,
                    LogCategory.Warning => Serilog.Events.LogEventLevel.Warning,
                    LogCategory.Error => Serilog.Events.LogEventLevel.Error,
                    LogCategory.Fatal => Serilog.Events.LogEventLevel.Fatal,
                    _ => Serilog.Events.LogEventLevel.Information
                };

                if (logEvent.Error == null)
                {
                    logger.Write(logLevel, "{Message}", logEvent.MessageText);
                }
                else
                {
                    logger.Write(logLevel, logEvent.Error, "{Message}", logEvent.MessageText);
                }
            }

            public override string CorrelationId => "system/" + Environment.MachineName;
        }
    }
}