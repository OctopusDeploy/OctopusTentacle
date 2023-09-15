using System;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Serilog;
using Log = Octopus.Tentacle.Diagnostics.Log;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TestSerilogILoggerILog : Log
    {
        private ILogger logger;
        
        public TestSerilogILoggerILog(ILogger logger)
        {
            this.logger = logger;
        }

        public override bool IsEnabled(LogCategory category) => true;

        protected override void WriteEvent(LogEvent logEvent)
        {
            if (logEvent.Error == null)
            {
                logger.Information("{LogCategory} {Message}", logEvent.Category, logEvent.MessageText);
            }
            else
            {
                logger.Error(logEvent.Error, "{LogCategory} {Message}", logEvent.Category, logEvent.MessageText);
            }
            
            if (logEvent.Error != null) Console.WriteLine(logEvent.Error);
        }

        public override void Flush()
        {
        }

        public override string CorrelationId => "system/" + Environment.MachineName;
    }

    public static class ILoggerExtensionMethods
    {
        public static Log ToILog(this ILogger logger)
        {
            return new TestSerilogILoggerILog(logger);
        }
    }
}