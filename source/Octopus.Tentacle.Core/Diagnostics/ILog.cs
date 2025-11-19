using System;

namespace Octopus.Tentacle.Core.Diagnostics
{
    public interface ILog
    {
        string CorrelationId { get; }

        /// <summary>
        /// Adds additional sensitive-variables to the LogContext.
        /// </summary>
        void WithSensitiveValues(string[] sensitiveValues);

        /// <summary>
        /// Adds an additional sensitive-variable to the LogContext.
        /// </summary>
        void WithSensitiveValue(string sensitiveValue);

        void Trace(string messageText);
        void Trace(Exception error);
        void Trace(Exception error, string messageText);

        void Verbose(string messageText);
        void Verbose(Exception error);
        void Verbose(Exception error, string messageText);

        void Info(string messageText);
        void Info(Exception error);
        void Info(Exception error, string messageText);

        void Warn(string messageText);
        void Warn(Exception error);
        void Warn(Exception error, string messageText);

        void Error(string messageText);
        void Error(Exception error);
        void Error(Exception error, string messageText);

        void Fatal(string messageText);
        void Fatal(Exception error);
        void Fatal(Exception error, string messageText);

        void Write(LogCategory category, string messageText);
        void Write(LogCategory category, Exception error);
        void Write(LogCategory category, Exception error, string messageText);

        [StringFormatMethod("messageFormat")]
        void WriteFormat(LogCategory category, string messageFormat, params object[] args);

        [StringFormatMethod("messageFormat")]
        void WriteFormat(LogCategory category, Exception error, string messageFormat, params object[] args);

        [StringFormatMethod("messageFormat")]
        void TraceFormat(string messageFormat, params object[] args);

        [StringFormatMethod("format")]
        void TraceFormat(Exception error, string format, params object[] args);

        [StringFormatMethod("messageFormat")]
        void VerboseFormat(string messageFormat, params object[] args);

        [StringFormatMethod("format")]
        void VerboseFormat(Exception error, string format, params object[] args);

        [StringFormatMethod("messageFormat")]
        void InfoFormat(string messageFormat, params object[] args);

        [StringFormatMethod("format")]
        void InfoFormat(Exception error, string format, params object[] args);

        [StringFormatMethod("messageFormat")]
        void WarnFormat(string messageFormat, params object[] args);

        [StringFormatMethod("format")]
        void WarnFormat(Exception error, string format, params object[] args);

        [StringFormatMethod("messageFormat")]
        void ErrorFormat(string messageFormat, params object[] args);

        [StringFormatMethod("format")]
        void ErrorFormat(Exception error, string format, params object[] args);

        [StringFormatMethod("messageFormat")]
        void FatalFormat(string messageFormat, params object[] args);

        [StringFormatMethod("format")]
        void FatalFormat(Exception error, string format, params object[] args);

        void Flush();
    }
}