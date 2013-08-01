using System;
using Newtonsoft.Json;
using Pipefish;

namespace Octopus.Shared.Communications.Logging
{
    public class LogMessage : IMessage
    {
        readonly string correlationId;
        readonly ActivityLogCategory category;
        private readonly DateTimeOffset occurred;
        readonly string messageText;

        public LogMessage(string correlationId, ActivityLogCategory category, string messageText) : this(correlationId, category, DateTimeOffset.UtcNow, messageText)
        {
        }

        [JsonConstructor]
        public LogMessage(string correlationId, ActivityLogCategory category, DateTimeOffset occurred, string messageText)
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

        public ActivityLogCategory Category
        {
            get { return category; }
        }

        public string MessageText
        {
            get { return messageText; }
        }
    }
}