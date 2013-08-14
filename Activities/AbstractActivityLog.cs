using System;
using Octopus.Shared.Orchestration.Logging;
using Octopus.Shared.Platform.Logging;
using Octopus.Shared.Util;

namespace Octopus.Shared.Activities
{
    public abstract class AbstractActivityLog : ITrace
    {
        protected abstract void WriteEvent(TraceCategory category, Exception error, string messageText);

        public void Write(TraceCategory category, string messageText)
        {
            Write(category, null, messageText);
        }

        public void Write(TraceCategory category, Exception error, string messageText)
        {
            WriteEvent(category, error != null ? error.GetRootError() : null, messageText);
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

        void Write(TraceCategory level, string format, object[] args)
        {
            var message = string.Format(format, args);
            Write(level, message);
        }

        public void UpdateProgressFormat(int progressPercentage, string messageFormat, params object[] args)
        {
            UpdateProgress(progressPercentage, string.Format(messageFormat, args));
        }

        public void UpdateProgress(int progressPercentage, string messageText)
        {
            VerboseFormat("{0} ({1}%)", messageText, progressPercentage);
        }
    }
}