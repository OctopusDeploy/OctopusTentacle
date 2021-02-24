using System;
using Octopus.CoreUtilities.Extensions;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics.Formatters;
using Octopus.Shared.Util;

namespace Octopus.Shared.Diagnostics
{
    public abstract class AbstractLog : ILog, IDisposable
    {
        protected AbstractLog(string[]? sensitiveValues = null)
        {
            SensitiveValueMasker = new SensitiveValueMasker(sensitiveValues);
        }

        public abstract string CorrelationId { get; }

        protected SensitiveValueMasker SensitiveValueMasker { get; }

        public virtual void Dispose()
        {
            SensitiveValueMasker.Flush();
            Flush();
        }

        public void WithSensitiveValues(string[] sensitiveValues)
        {
            SensitiveValueMasker.WithSensitiveValues(sensitiveValues);
        }

        public void WithSensitiveValue(string sensitiveValue)
        {
            SensitiveValueMasker.WithSensitiveValue(sensitiveValue);
        }

        protected abstract void WriteEvent(LogEvent logEvent);

        public virtual bool IsEnabled(LogCategory category)
            => true;

        public void Write(LogCategory category, string messageText)
        {
            if (IsEnabled(category))
                Write(category, null, messageText);
        }

        public void Write(LogCategory category, Exception error)
        {
            if (error == null)
                return;

            if (IsEnabled(category))
                Write(category, error, error.PrettyPrint(false));
        }

        public virtual void Write(LogCategory category, Exception? error, string messageText)
        {
            if (IsEnabled(category))
                SensitiveValueMasker.SafeSanitize(messageText,
                    sanitized => WriteEvent(new LogEvent(CorrelationId, category, sanitized, error?.UnpackFromContainers())));
        }

        public void WriteFormat(LogCategory category, string messageFormat, params object[] args)
        {
            WriteFormat(category, null, messageFormat, args);
        }

        public void WriteFormat(LogCategory category, Exception? error, string messageFormat, params object[] args)
        {
            if (!IsEnabled(category))
                return;

            SensitiveValueMasker.SafeSanitize(SafeFormat(messageFormat, args),
                sanitized => WriteEvent(new LogEvent(CorrelationId, category, sanitized, error?.UnpackFromContainers())));
        }

        static string SafeFormat(string messageFormat, object[] args)
        {
            try
            {
                return string.Format(new PluralStringFormatter(), messageFormat, args);
            }
            catch (Exception ex)
            {
                return (messageFormat ?? "") + " (" + string.Join(",", (args ?? new object[0]).WhereNotNull()) + ") => " + ex.Message;
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
            SensitiveValueMasker.SafeSanitize(messageText,
                sanitized => WriteEvent(new LogEvent(CorrelationId,
                    LogCategory.Progress,
                    sanitized,
                    null,
                    progressPercentage)));
        }

        public void UpdateProgressFormat(int progressPercentage, string messageFormat, params object[] args)
        {
            UpdateProgress(progressPercentage, string.Format(messageFormat, args));
        }

        public abstract void Flush();
    }
}