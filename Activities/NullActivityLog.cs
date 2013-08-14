using System;
using System.Text;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Activities
{
    public class NullActivityLog : AbstractActivityLog
    {
        readonly ILog log;
        readonly StringBuilder logText = new StringBuilder();

        public NullActivityLog() : this(null)
        {
        }

        public NullActivityLog(ILog log)
        {
            this.log = log ?? Log.Octopus();
        }

        protected override void WriteEvent(TraceCategory level, Exception error, string messageText)
        {
            logText.AppendLine(level + " " + messageText + " " + error);
            log.Write(level, error, messageText);
        }

        public string GetLog()
        {
            return logText.ToString();
        }
    }
}