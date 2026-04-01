using System;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Serilog;
using Serilog.Events;
using LogEvent = Octopus.Tentacle.Diagnostics.LogEvent;

namespace Octopus.Tentacle.CommonTestUtils.Diagnostics
{
    public class SerilogSystemLog : SystemLog
    {
        readonly ILogger logger;

        public SerilogSystemLog(ILogger logger)
        {
            this.logger = logger;
        }

        protected override void WriteEvent(LogEvent logEvent)
        {
            var level = ToSerilogLevel(logEvent.Category);
            if (logEvent.Error != null)
                logger.Write(level, logEvent.Error, logEvent.MessageText);
            else
                logger.Write(level, logEvent.MessageText);
        }

        static LogEventLevel ToSerilogLevel(LogCategory category) => category switch
        {
            LogCategory.Trace => LogEventLevel.Verbose,
            LogCategory.Verbose => LogEventLevel.Debug,
            LogCategory.Info => LogEventLevel.Information,
            LogCategory.Warning => LogEventLevel.Warning,
            LogCategory.Error => LogEventLevel.Error,
            LogCategory.Fatal => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}
