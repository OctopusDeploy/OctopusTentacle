using System;
using Octopus.Shared.Orchestration.Logging;
using Octopus.Shared.Platform.Logging;
using Octopus.Shared.Util;

namespace Octopus.Shared.Activities
{
    public abstract class AbstractActivityLog : ITrace
    {
        public void Verbose(object message)
        {
            Write(TraceCategory.Verbose, message);
        }

        public void Verbose(object message, Exception exception)
        {
            Write(TraceCategory.Verbose, message, exception);
        }

        public void Write(TraceCategory category, string messageText)
        {
            Write(category, (object)messageText);
        }

        public void Write(TraceCategory category, Exception error, string messageText)
        {
            Write(category, messageText, error);
        }

        public void WriteFormat(TraceCategory category, string messageFormat, params object[] args)
        {
            Write(category, messageFormat, args);
        }

        public void Trace(string messageText)
        {
            Write(TraceCategory.Trace, messageText);
        }

        public void TraceFormat(string messageFormat, params object[] args)
        {
            Write(TraceCategory.Trace, messageFormat, args);
        }

        public void Verbose(string messageText)
        {
            Write(TraceCategory.Verbose, messageText);
        }

        public void VerboseFormat(string format, params object[] args)
        {
            Write(TraceCategory.Verbose, format, args);
        }

        public void Info(string messageText)
        {
            Write(TraceCategory.Info, messageText);
        }

        public void Info(object message)
        {
            Write(TraceCategory.Info, message);
        }

        public void Info(object message, Exception exception)
        {
            Write(TraceCategory.Info, message, exception);
        }

        public void InfoFormat(string format, params object[] args)
        {
            Write(TraceCategory.Info, format, args);
        }

        public void Alert(string messageText)
        {
            Write(TraceCategory.Alert, messageText);
        }

        public void Alert(Exception error, string messageText)
        {
            Write(TraceCategory.Alert, error, messageText);
        }

        public void AlertFormat(string messageFormat, params object[] args)
        {
            Write(TraceCategory.Alert, messageFormat, args);
        }

        public void Warn(string messageText)
        {
            Write(TraceCategory.Warning, messageText);
        }

        public void Warn(Exception error, string messageText)
        {
            Write(TraceCategory.Warning, error, messageText);
        }

        public void Warn(object message)
        {
            Write(TraceCategory.Warning, message);
        }

        public void Warn(object message, Exception exception)
        {
            Write(TraceCategory.Warning, message, exception);
        }

        public void WarnFormat(string format, params object[] args)
        {
            Write(TraceCategory.Warning, format, args);
        }

        public void Error(string messageText)
        {
            Write(TraceCategory.Error, messageText);
        }

        public void Error(Exception error, string messageText)
        {
            Write(TraceCategory.Error, error, messageText);
        }

        public void Error(object message)
        {
            Write(TraceCategory.Error, message);
        }

        public void Error(object message, Exception exception)
        {
            Write(TraceCategory.Error, message, exception);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            Write(TraceCategory.Error, format, args);
        }

        public void Fatal(string messageText)
        {
            Write(TraceCategory.Fatal, messageText);
        }

        public void Fatal(Exception error, string messageText)
        {
            Write(TraceCategory.Fatal, error, messageText);
        }

        public void FatalFormat(string messageFormat, params object[] args)
        {
            Write(TraceCategory.Alert, messageFormat, args);
        }

        public abstract void Write(TraceCategory level, object message);

        void Write(TraceCategory level, string format, object[] args)
        {
            var message = string.Format(format, args);
            Write(level, message);
        }

        void Write(TraceCategory level, object message, Exception ex)
        {
            Write(level, message + " " + ex.GetRootError());
        }
    }
}