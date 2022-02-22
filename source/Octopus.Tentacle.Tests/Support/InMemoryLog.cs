using System;
using System.Text;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;

namespace Octopus.Tentacle.Tests.Support
{
    public class InMemoryLog : SystemLog
    {
        private readonly ISystemLog log;
        private readonly StringBuilder logText = new StringBuilder();

        public InMemoryLog() : this(null)
        {
        }

        public InMemoryLog(ISystemLog log)
        {
            this.log = log ?? new SystemLog();
        }

        protected override void WriteEvent(LogEvent logEvent)
        {
            logText.AppendLine(logEvent.Category + " " + logEvent.MessageText + " " + logEvent.Error);
            log.Write(logEvent.Category, logEvent.Error, logEvent.MessageText);
        }

        public override void Flush()
        {
        }

        public override bool IsEnabled(LogCategory category)
        {
            return true;
        }

        public string GetLog()
        {
            return logText.ToString();
        }
    }
}