using System;
using System.Text;
using Octopus.Shared.Platform.Logging;
using Octopus.Shared.Time;

namespace Octopus.Shared.Activities
{
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

        public override void Write(TraceCategory level, object message)
        {
            var now = clock.GetUtcTime();
            var formatted = now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level.ToString().ToUpper().PadRight(6, ' ') + " " + message;
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