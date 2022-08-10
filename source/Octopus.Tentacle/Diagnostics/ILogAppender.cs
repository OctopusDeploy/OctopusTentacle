using System;

namespace Octopus.Tentacle.Diagnostics
{
    public interface ILogAppender
    {
        void WriteEvent(LogEvent logEvent);
        void Flush();
    }
}