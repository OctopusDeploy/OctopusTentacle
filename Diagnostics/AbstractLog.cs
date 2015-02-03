using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Shared.Logging;
using Octopus.Shared.Util;

namespace Octopus.Shared.Diagnostics
{
    public abstract class AbstractLog : ILog
    {
        protected abstract void WriteEvent(LogEvent logEvent);
        protected abstract void WriteEvents(IList<LogEvent> logEvents);
        public abstract IDisposable WithinBlock(LogCorrelator logger);
        public abstract LogCorrelator Current { get; }

        public IDisposable OpenBlock(string messageText)
        {
            var child = Current.CreateChild();
            var context = WithinBlock(child);
            Write(LogCategory.Info, messageText);
            return context;
        }

        public IDisposable OpenBlock(string messageFormat, params object[] args)
        {
            return OpenBlock(string.Format(messageFormat, args));
        }

        public LogCorrelator PlanFutureBlock(string messageText)
        {
            var child = Current.CreateChild();
            using (WithinBlock(child))
            {
                Write(LogCategory.Planned, messageText);
            }
            return child;
        }

        public LogCorrelator PlanFutureBlock(string messageFormat, params object[] args)
        {
            return PlanFutureBlock(string.Format(messageFormat, args));
        }

        public void Abandon()
        {
            Write(LogCategory.Abandoned, "Abandon");
        }

        public void Reinstate()
        {
            Write(LogCategory.Planned, "Reinstate");
        }

        public void Finish()
        {
            Write(LogCategory.Finished, "Finished");
        }

        public void Merge(IEnumerable<LogEvent> logEvents)
        {
            WriteEvents(logEvents.Where(l => l != null).Select(l => new LogEvent(Current.CorrelationId + "/" + l.CorrelationId, l.Category, l.MessageText, l.Error, l.Occurred, l.ProgressPercentage)).ToList());
        }

        public virtual bool IsEnabled(LogCategory category)
        {
            return true;
        }

        public bool IsVerboseEnabled { get { return IsEnabled(LogCategory.Verbose); } }
        public bool IsErrorEnabled { get { return IsEnabled(LogCategory.Error); } }
        public bool IsFatalEnabled { get { return IsEnabled(LogCategory.Fatal); } }
        public bool IsInfoEnabled { get { return IsEnabled(LogCategory.Info); } }
        public bool IsTraceEnabled { get { return IsEnabled(LogCategory.Trace); } }
        public bool IsWarnEnabled { get { return IsEnabled(LogCategory.Warning); } }

        public void Write(LogCategory category, string messageText)
        {
            if (IsEnabled(category))
            {
                Write(category, null, messageText);
            }
        }

        public void Write(LogCategory category, Exception error)
        {
            if (error == null)
                return;

            if (IsEnabled(category))
            {
                Write(category, error, error.GetErrorSummary());
            }
        }

        public void Write(LogCategory category, Exception error, string messageText)
        {
            if (IsEnabled(category))
            {
                WriteEvent(new LogEvent(Current.CorrelationId, category, messageText, error != null ? error.UnpackFromContainers() : null));
            }
        }

        public void WriteFormat(LogCategory category, string messageFormat, params object[] args)
        {
            WriteFormat(category, null, messageFormat, args);
        }

        public void WriteFormat(LogCategory category, Exception error, string messageFormat, params object[] args)
        {
            if (!IsEnabled(category)) 
                return;
            
            var message = SafeFormat(messageFormat, args);

            WriteEvent(new LogEvent(Current.CorrelationId, category, message, error != null ? error.UnpackFromContainers() : null));
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
            Write(LogCategory.Trace, messageText);
        }

        public void Trace(Exception error)
        {
            Write(LogCategory.Trace, error);
        }

        public void Trace(Exception error, string message)
        {
            Write(LogCategory.Trace, error, message);
        }

        public void TraceFormat(string messageFormat, params object[] args)
        {
            WriteFormat(LogCategory.Trace, messageFormat, args);
        }

        public void TraceFormat(Exception error, string format, params object[] args)
        {
            WriteFormat(LogCategory.Trace, error, format, args);
        }

        public void Verbose(string messageText)
        {
            Write(LogCategory.Verbose, messageText);
        }

        public void Verbose(Exception error)
        {
            Write(LogCategory.Verbose, error);
        }

        public void Verbose(Exception error, string message)
        {
            Write(LogCategory.Verbose, error, message);
        }

        public void VerboseFormat(string format, params object[] args)
        {
            WriteFormat(LogCategory.Verbose, format, args);
        }

        public void VerboseFormat(Exception error, string format, params object[] args)
        {
            WriteFormat(LogCategory.Verbose, error, format, args);
        }

        public void Info(string messageText)
        {
            Write(LogCategory.Info, messageText);
        }

        public void Info(Exception error)
        {
            Write(LogCategory.Info, error);
        }

        public void Info(Exception error, string message)
        {
            Write(LogCategory.Info, error, message);
        }

        public void InfoFormat(string format, params object[] args)
        {
            WriteFormat(LogCategory.Info, format, args);
        }

        public void InfoFormat(Exception error, string format, params object[] args)
        {
            WriteFormat(LogCategory.Info, error, format, args);
        }

        public void Warn(string messageText)
        {
            Write(LogCategory.Warning, messageText);
        }

        public void Warn(Exception error)
        {
            Write(LogCategory.Warning, error);
        }

        public void Warn(Exception error, string messageText)
        {
            Write(LogCategory.Warning, error, messageText);
        }

        public void WarnFormat(string format, params object[] args)
        {
            WriteFormat(LogCategory.Warning, format, args);
        }

        public void WarnFormat(Exception exception, string format, params object[] args)
        {
            WriteFormat(LogCategory.Warning, exception, format, args);
        }

        public void Error(string messageText)
        {
            Write(LogCategory.Error, messageText);
        }

        public void Error(Exception error)
        {
            Write(LogCategory.Error, error);
        }

        public void Error(Exception error, string messageText)
        {
            Write(LogCategory.Error, error, messageText);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            WriteFormat(LogCategory.Error, format, args);
        }

        public void ErrorFormat(Exception exception, string format, params object[] args)
        {
            WriteFormat(LogCategory.Error, exception, format, args);
        }

        public void Fatal(string messageText)
        {
            Write(LogCategory.Fatal, messageText);
        }

        public void Fatal(Exception error)
        {
            Write(LogCategory.Fatal, error);
        }

        public void Fatal(Exception error, string messageText)
        {
            Write(LogCategory.Fatal, error, messageText);
        }

        public void FatalFormat(string messageFormat, params object[] args)
        {
            WriteFormat(LogCategory.Fatal, messageFormat, args);
        }

        public void FatalFormat(Exception exception, string format, params object[] args)
        {
            WriteFormat(LogCategory.Fatal, exception, format, args);
        }

        public void UpdateProgress(int progressPercentage, string messageText)
        {
            WriteEvent(new LogEvent(Current.CorrelationId, LogCategory.Progress, messageText, null, progressPercentage));
        }

        public void UpdateProgressFormat(int progressPercentage, string messageFormat, params object[] args)
        {
            UpdateProgress(progressPercentage, string.Format(messageFormat, args));
        }
    }
}