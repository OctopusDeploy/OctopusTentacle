using System;
using System.Text;
using Octopus.Shared.Diagnostics;

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

        public override void Write(ActivityLogLevel level, object message)
        {
            logText.AppendLine(level + " " + message);

            var messageText = (message ?? string.Empty).ToString();
            switch (level)
            {
                case ActivityLogLevel.Debug:
                    log.Debug(messageText);
                    break;
                case ActivityLogLevel.Info:
                    log.Info(messageText);
                    break;
                case ActivityLogLevel.Warn:
                    log.Warn(messageText);
                    break;
                case ActivityLogLevel.Error:
                    log.Error(messageText);
                    break;
            }
        }

        public override IActivityLog OverwritePrevious()
        {
            return this;
        }

        public override string GetLog()
        {
            return logText.ToString();
        }
    }
}