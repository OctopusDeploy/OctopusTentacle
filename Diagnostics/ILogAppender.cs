using System;
using System.Collections.Generic;

namespace Octopus.Shared.Diagnostics
{
    public interface ILogAppender
    {
        void WriteEvent(LogEvent logEvent);
        void WriteEvents(IList<LogEvent> logEvents);
        void Mask(string correlationId, IList<string> sensitiveValues);
        void Flush();
    }
}