using System;
using System.Linq;
using Octopus.Shared.Util;

namespace Octopus.Shared.Diagnostics
{
    public abstract class AbstractLog : ILog
    {
        protected abstract void WriteEvent(TraceCategory category, Exception error, string messageText);

        public abstract ILog BeginOperation(string messageText);
        public abstract void EndOperation();
        public abstract void UpdateProgress(int progressPercentage, string messageText);
        public abstract bool IsEnabled(TraceCategory category);

        public bool IsVerboseEnabled { get { return IsEnabled(TraceCategory.Verbose); } }
        public bool IsErrorEnabled { get { return IsEnabled(TraceCategory.Error); } }
        public bool IsFatalEnabled { get { return IsEnabled(TraceCategory.Fatal); } }
        public bool IsInfoEnabled { get { return IsEnabled(TraceCategory.Info); } }
        public bool IsTraceEnabled { get { return IsEnabled(TraceCategory.Trace); } }
        public bool IsWarnEnabled { get { return IsEnabled(TraceCategory.Warning); } }

        public void Write(TraceCategory category, string messageText)
        {
            if (IsEnabled(category))
            {
                Write(category, null, messageText);
            }
        }

        public void Write(TraceCategory category, Exception error)
        {
            if (error == null)
                return;

            if (IsEnabled(category))
            {
                Write(category, error, error.GetErrorSummary());
            }
        }

        public void Write(TraceCategory category, Exception error, string messageText)
        {
            if (IsEnabled(category))
            {
                WriteEvent(category, error != null ? error.UnpackFromContainers() : null, messageText);
            }
        }

        public void WriteFormat(TraceCategory category, string messageFormat, params object[] args)
        {
            WriteFormat(category, null, messageFormat, args);
        }

        public void WriteFormat(TraceCategory category, Exception error, string messageFormat, params object[] args)
        {
            if (!IsEnabled(category)) 
                return;
            
            var message = SafeFormat(messageFormat, args);

            WriteEvent(category, error, message);
        }

        public static string SafeFormat(string messageFormat, object[] args)
        {
            try
            {
                return string.Format(messageFormat, args);
            }
            catch (Exception ex)
            {
                return (messageFormat ?? "") + " (" + string.Join(",", (args ?? new object[0]).Where(o => o != null)) + ") => " + ex.Message;
            }
        }

        public void Trace(string messageText)
        {
            Write(TraceCategory.Trace, messageText);
        }

        public void Trace(Exception error)
        {
            Write(TraceCategory.Trace, error);
        }

        public void Trace(Exception error, string message)
        {
            Write(TraceCategory.Trace, error, message);
        }

        public void TraceFormat(string messageFormat, params object[] args)
        {
            WriteFormat(TraceCategory.Trace, messageFormat, args);
        }

        public void TraceFormat(Exception error, string format, params object[] args)
        {
            WriteFormat(TraceCategory.Trace, error, format, args);
        }

        public void Verbose(string messageText)
        {
            Write(TraceCategory.Verbose, messageText);
        }

        public void Verbose(Exception error)
        {
            Write(TraceCategory.Verbose, error);
        }

        public void Verbose(Exception error, string message)
        {
            Write(TraceCategory.Verbose, error, message);
        }

        public void VerboseFormat(string format, params object[] args)
        {
            WriteFormat(TraceCategory.Verbose, format, args);
        }

        public void VerboseFormat(Exception error, string format, params object[] args)
        {
            WriteFormat(TraceCategory.Verbose, error, format, args);
        }

        public void Info(string messageText)
        {
            Write(TraceCategory.Info, messageText);
        }

        public void Info(Exception error)
        {
            Write(TraceCategory.Info, error);
        }

        public void Info(Exception error, string message)
        {
            Write(TraceCategory.Info, error, message);
        }

        public void InfoFormat(string format, params object[] args)
        {
            WriteFormat(TraceCategory.Info, format, args);
        }

        public void InfoFormat(Exception error, string format, params object[] args)
        {
            WriteFormat(TraceCategory.Info, error, format, args);
        }

        public void Warn(string messageText)
        {
            Write(TraceCategory.Warning, messageText);
        }

        public void Warn(Exception error)
        {
            Write(TraceCategory.Warning, error);
        }

        public void Warn(Exception error, string messageText)
        {
            Write(TraceCategory.Warning, error, messageText);
        }

        public void WarnFormat(string format, params object[] args)
        {
            WriteFormat(TraceCategory.Warning, format, args);
        }

        public void WarnFormat(Exception exception, string format, params object[] args)
        {
            WriteFormat(TraceCategory.Warning, exception, format, args);
        }

        public void Error(string messageText)
        {
            Write(TraceCategory.Error, messageText);
        }

        public void Error(Exception error)
        {
            Write(TraceCategory.Error, error);
        }

        public void Error(Exception error, string messageText)
        {
            Write(TraceCategory.Error, error, messageText);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            WriteFormat(TraceCategory.Error, format, args);
        }

        public void ErrorFormat(Exception exception, string format, params object[] args)
        {
            WriteFormat(TraceCategory.Error, exception, format, args);
        }

        public void Fatal(string messageText)
        {
            Write(TraceCategory.Fatal, messageText);
        }

        public void Fatal(Exception error)
        {
            Write(TraceCategory.Fatal, error);
        }

        public void Fatal(Exception error, string messageText)
        {
            Write(TraceCategory.Fatal, error, messageText);
        }

        public void FatalFormat(string messageFormat, params object[] args)
        {
            WriteFormat(TraceCategory.Fatal, messageFormat, args);
        }

        public void FatalFormat(Exception exception, string format, params object[] args)
        {
            WriteFormat(TraceCategory.Fatal, exception, format, args);
        }

        public ILog BeginOperationFormat(string messageFormat, params object[] args)
        {
            return BeginOperation(string.Format(messageFormat, args));
        }

        public void UpdateProgressFormat(int progressPercentage, string messageFormat, params object[] args)
        {
            UpdateProgress(progressPercentage, string.Format(messageFormat, args));
        }
    }
}