using System;
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
            var scope = ThreadContext.Properties["LogOutputTo"] as ILogScope;
            if (scope == null)
                return;

            scope.Log(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " " + loggingEvent.Level.DisplayName.PadRight(6, ' ') + " " + loggingEvent.RenderedMessage);
            
            if (loggingEvent.ExceptionObject != null)
            {
                scope.Log(loggingEvent.ExceptionObject.ToString());
            }
        }

        public void Close()
        {
        }
    }
}