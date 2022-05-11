using System;

namespace Octopus.Shared.Diagnostics
{
    public interface ILogAppender
    {
        void WriteEvent(LogEvent logEvent);
        void Flush();
    }
}