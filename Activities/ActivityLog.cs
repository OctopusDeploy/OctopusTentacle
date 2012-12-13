using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Shared.Util;
using log4net.Core;

namespace Octopus.Shared.Activities
{
    public class ActivityLog : AbstractActivityLog
    {
        readonly StringBuilder log = new StringBuilder();
        string mostRecentLine;
        readonly object sync = new object();

        protected override void Write(Level level, object message)
        {
            var formatted = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " " + level.DisplayName.PadRight(6, ' ') + " " + message;
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