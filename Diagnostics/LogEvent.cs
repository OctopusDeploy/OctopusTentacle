using System;
using System.Threading;
using Newtonsoft.Json;

namespace Octopus.Shared.Diagnostics
{
    public class LogEvent
    {
        readonly string correlationId;
        readonly LogCategory category;
        readonly string messageText;
        readonly Exception error;
        readonly DateTimeOffset occurred;
        readonly int progressPercentage;
        readonly long sequence;
        static long nextSequence;

        public LogEvent(string correlationId, LogCategory category, string messageText, Exception error) : this(correlationId, category, messageText, error, 0)
        {
        }

        public LogEvent(string correlationId, LogCategory category, string messageText, Exception error, int progressPercentage) : this(correlationId, category, messageText, error, DateTimeOffset.UtcNow, progressPercentage)
        {
        }

        public LogEvent(string correlationId, LogCategory category, string messageText, Exception error, DateTimeOffset occurred) : this(correlationId, category, messageText, error, occurred, 0)
        {
        }

        public LogEvent(string correlationId, LogCategory category, string messageText, Exception error, DateTimeOffset occurred, int progressPercentage) : this(0, correlationId, category, messageText, error, occurred, progressPercentage)
        {
            sequence = Interlocked.Increment(ref nextSequence);
        }

        [JsonConstructor]
        LogEvent(int sequence, string correlationId, LogCategory category, string messageText, Exception error, DateTimeOffset occurred, int progressPercentage)
        {
            this.sequence = sequence;
            this.correlationId = correlationId;
            this.category = category;
            this.messageText = messageText;
            this.error = error;
            this.occurred = occurred;
            this.progressPercentage = progressPercentage;
        }

        public long Sequence
        {
            get { return sequence; }
        }

        public string CorrelationId
        {
            get { return correlationId; }
        }

        public LogCategory Category
        {
            get { return category; }
        }

        public string MessageText
        {
            get { return messageText; }
        }

        public Exception Error
        {
            get { return error; }
        }

        public int ProgressPercentage
        {
            get { return progressPercentage; }
        }

        public DateTimeOffset Occurred
        {
            get { return occurred; }
        }
    }
}