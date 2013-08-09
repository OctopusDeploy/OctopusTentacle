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

        public override void Write(TraceCategory level, object message)
        {
            logText.AppendLine(level + " " + message);
            log.Write(level, (message ?? "").ToString());
        }

        public string GetLog()
        {
            return logText.ToString();
        }
    }
}