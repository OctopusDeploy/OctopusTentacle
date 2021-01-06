#nullable enable
using System;
using Newtonsoft.Json;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Diagnostics
{
    public class LogEvent
    {
        public LogEvent(string correlationId, LogCategory category, string messageText, Exception? error) : this(correlationId,
            category,
            messageText,
            error,
            0)
        {
        }

        public LogEvent(string correlationId,
            LogCategory category,
            string messageText,
            Exception? error,
            int progressPercentage) : this(correlationId,
            category,
            messageText,
            error,
            DateTimeOffset.UtcNow,
            progressPercentage)
        {
        }

        public LogEvent(string correlationId,
            LogCategory category,
            string messageText,
            Exception? error,
            DateTimeOffset occurred) : this(correlationId,
            category,
            messageText,
            error,
            occurred,
            0)
        {
        }

        [JsonConstructor]
        public LogEvent(string correlationId,
            LogCategory category,
            string messageText,
            Exception? error,
            DateTimeOffset occurred,
            int progressPercentage)
        {
            CorrelationId = correlationId;
            Category = category;
            MessageText = messageText;
            Error = error;
            Occurred = occurred;
            ProgressPercentage = progressPercentage;
        }

        public string CorrelationId { get; }

        public LogCategory Category { get; }

        public string MessageText { get; }

        public Exception? Error { get; }

        public int ProgressPercentage { get; }

        public DateTimeOffset Occurred { get; }
    }
}