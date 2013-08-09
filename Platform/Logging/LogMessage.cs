using System;
using Newtonsoft.Json;
using Pipefish;

namespace Octopus.Shared.Platform.Logging
{
    public class LogMessage : IMessage
    {
        readonly string correlationId;
        readonly TraceCategory category;
        private readonly DateTimeOffset occurred;
        readonly string messageText;

        public LogMessage(string correlationId, TraceCategory category, string messageText) : this(correlationId, category, DateTimeOffset.UtcNow, messageText)
        {
        }

        [JsonConstructor]
        public LogMessage(string correlationId, TraceCategory category, DateTimeOffset occurred, string messageText)
        {
            if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentNullException("correlationId");
            if (string.IsNullOrWhiteSpace(messageText)) throw new ArgumentNullException("messageText");

            this.correlationId = correlationId;
            this.category = category;
            this.occurred = occurred;
            this.messageText = messageText;
        }

        public string CorrelationId
        {
            get { return correlationId; }
        }

        public DateTimeOffset Occurred
        {
            get { return occurred; }
        }

        public TraceCategory Category
        {
            get { return category; }
        }

        public string MessageText
        {
            get { return messageText; }
        }
    }
}