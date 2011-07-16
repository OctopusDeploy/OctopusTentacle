using System;
using System.Text;
using log4net;
using log4net.Appender;
using log4net.Core;

namespace Octopus.Shared.Diagnostics
{
    public class LogTapAppender : IAppender
    {
        public string Name { get; set; }

        public void DoAppend(LoggingEvent loggingEvent)
        {
            var capture = ThreadContext.Properties["LogOutputTo"] as StringBuilder;
            if (capture == null)
                return;

            capture.AppendLine(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " " + loggingEvent.Level.DisplayName.PadRight(6, ' ') + " " + loggingEvent.RenderedMessage);
            if (loggingEvent.ExceptionObject != null)
            {
                capture.Append(loggingEvent.ExceptionObject.ToString());
                capture.AppendLine();
            }
        }

        public void Close()
        {
        }
    }
}