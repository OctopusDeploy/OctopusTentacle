using System;
using System.Text;
using Octopus.Shared.Platform.Logging;
using Octopus.Shared.Time;

namespace Octopus.Shared.Activities
{
    // To be removed along with activities subsystem
    public class ActivityLog : AbstractActivityLog
    {
        readonly IClock clock;
        readonly StringBuilder log = new StringBuilder();
        string mostRecentLine;
        readonly object sync = new object();

        public ActivityLog() : this(null)
        {
        }

        public ActivityLog(IClock clock)
        {
            this.clock = clock ?? new SystemClock();
        }

        protected override void WriteEvent(TraceCategory level, Exception error, string messageText)
        {
            var now = clock.GetUtcTime();
            var formatted = now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level.ToString().ToUpper().PadRight(6, ' ') + " " + messageText + " " + error;
            lock (sync)
            {
                mostRecentLine = formatted;
                log.AppendLine(mostRecentLine);
            }
        }

        public string GetLog()
        {
            // For testing only, not thread safe
            return log.ToString();
        }
    }
}