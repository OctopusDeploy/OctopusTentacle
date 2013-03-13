using System;
using System.Text;
using Octopus.Shared.Time;
using log4net.Core;

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

        public override void Write(Level level, object message)
        {
            var now = clock.GetUtcTime();
            var formatted = now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level.DisplayName.PadRight(6, ' ') + " " + message;
            lock (sync)
            {
                mostRecentLine = formatted;
                log.AppendLine(mostRecentLine);
            }
        }

        public override IActivityLog OverwritePrevious()
        {
            lock (sync)
            {
                log.Length = log.Length - mostRecentLine.Length - 2;
            }

            return this;
        }

        public override string GetLog()
        {
            return log.ToString();
        }
    }
}