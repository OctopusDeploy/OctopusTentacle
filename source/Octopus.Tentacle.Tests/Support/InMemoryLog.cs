using System;
using System.Text;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;
using AbstractLog = Octopus.Tentacle.Diagnostics.AbstractLog;
using Log = Octopus.Tentacle.Diagnostics.Log;
using LogEvent = Octopus.Tentacle.Diagnostics.LogEvent;

namespace Octopus.Tentacle.Tests.Support
{
    public class InMemoryLog : AbstractLog
    {
        readonly ILog log;
        readonly StringBuilder logText = new StringBuilder();

        public InMemoryLog() : this(null)
        {
        }

        public InMemoryLog(ILog log)
        {
            this.log = log ?? Log.Octopus();
        }

        public override ILogContext CurrentContext => new LogContext();

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