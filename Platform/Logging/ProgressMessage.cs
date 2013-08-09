using System;
using Newtonsoft.Json;
using Pipefish;

namespace Octopus.Shared.Platform.Logging
{
    public class ProgressMessage : IMessage
    {
        readonly string correlationId;
        readonly ProgressMessageCategory category;
        readonly DateTimeOffset occurred;
        readonly int percentage;
        readonly string message;

        public ProgressMessage(string correlationId, ProgressMessageCategory category, int percentage, string message) : this(correlationId, category, DateTimeOffset.UtcNow, percentage, message)
        {
        }

        [JsonConstructor]
        public ProgressMessage(string correlationId, ProgressMessageCategory category, DateTimeOffset occurred, int percentage, string message)
        {
            this.correlationId = correlationId;
            this.category = category;
            this.occurred = occurred;
            this.percentage = percentage;
            this.message = message;
        }

        public string CorrelationId
        {
            get { return correlationId; }
        }

        public ProgressMessageCategory Category
        {
            get { return category; }
        }

        public DateTimeOffset Occurred
        {
            get { return occurred; }
        }

        public int Percentage
        {
            get { return percentage; }
        }

        public string Message
        {
            get { return message; }
        }
    }
}