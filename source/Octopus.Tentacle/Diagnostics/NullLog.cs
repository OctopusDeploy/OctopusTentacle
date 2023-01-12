using System;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Diagnostics
{
    public class NullLog : ISystemLog
    {
        public NullLog()
        {
            CorrelationId = Guid.NewGuid().ToString();
        }

        public string CorrelationId { get; }

        public void Dispose()
        {
        }

        public ISystemLog ChildContext(string[] sensitiveValues)
        {
            return new NullLog();
        }

        public void WithSensitiveValues(string[] sensitiveValues)
        {
        }

        public void WithSensitiveValue(string sensitiveValue)
        {
        }

        public void Trace(string messageText)
        {
        }

        public void Trace(Exception error)
        {
        }

        public void Trace(Exception error, string messageText)
        {
        }

        public void Verbose(string messageText)
        {
        }

        public void Verbose(Exception error)
        {
        }

        public void Verbose(Exception error, string messageText)
        {
        }

        public void Info(string messageText)
        {
        }

        public void Info(Exception error)
        {
        }

        public void Info(Exception error, string messageText)
        {
        }

        public void Warn(string messageText)
        {
        }

        public void Warn(Exception error)
        {
        }

        public void Warn(Exception error, string messageText)
        {
        }

        public void Error(string messageText)
        {
        }

        public void Error(Exception error)
        {
        }

        public void Error(Exception error, string messageText)
        {
        }

        public void Fatal(string messageText)
        {
        }

        public void Fatal(Exception error)
        {
        }

        public void Fatal(Exception error, string messageText)
        {
        }

        public void Write(LogCategory category, string messageText)
        {
        }

        public void Write(LogCategory category, Exception error)
        {
        }

        public void Write(LogCategory category, Exception error, string messageText)
        {
        }

        public void WriteFormat(LogCategory category, string messageFormat, params object[] args)
        {
        }

        public void WriteFormat(LogCategory category, Exception error, string messageFormat, params object[] args)
        {
        }

        public void TraceFormat(string messageFormat, params object[] args)
        {
        }

        public void TraceFormat(Exception error, string format, params object[] args)
        {
        }

        public void VerboseFormat(string messageFormat, params object[] args)
        {
        }

        public void VerboseFormat(Exception error, string format, params object[] args)
        {
        }

        public void InfoFormat(string messageFormat, params object[] args)
        {
        }

        public void InfoFormat(Exception error, string format, params object[] args)
        {
        }

        public void WarnFormat(string messageFormat, params object[] args)
        {
        }

        public void WarnFormat(Exception error, string format, params object[] args)
        {
        }

        public void ErrorFormat(string messageFormat, params object[] args)
        {
        }

        public void ErrorFormat(Exception error, string format, params object[] args)
        {
        }

        public void FatalFormat(string messageFormat, params object[] args)
        {
        }

        public void FatalFormat(Exception error, string format, params object[] args)
        {
        }

        public void Flush()
        {
        }
    }
}