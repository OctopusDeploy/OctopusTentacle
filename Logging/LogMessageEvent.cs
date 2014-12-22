using System;
using Newtonsoft.Json;
using Octopus.Shared.Diagnostics;
using Pipefish;

namespace Octopus.Shared.Logging
{
    public class LogMessageEvent : IMessage
    {
        readonly string correlationId;
        readonly TraceCategory category;
        readonly DateTimeOffset occurred;
        readonly string messageText;
        readonly string detail;

        public LogMessageEvent(string correlationId, TraceCategory category, string messageText, string detail = null) 
            : this(correlationId, category, DateTimeOffset.UtcNow, messageText, detail)
        {
        }

        [JsonConstructor]
        public LogMessageEvent(string correlationId, TraceCategory category, DateTimeOffset occurred, string messageText, string detail = null)
        {
            if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentNullException("correlationId");
            if (string.IsNullOrWhiteSpace(messageText)) throw new ArgumentNullException("messageText");

            this.correlationId = correlationId;
            this.category = category;
            this.occurred = occurred;
            this.messageText = messageText;
            this.detail = detail;
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

        public string Detail
        {
            get { return detail; }
        }
    }
}