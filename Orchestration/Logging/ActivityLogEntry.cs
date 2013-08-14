using System;
using Pipefish.Core;

namespace Octopus.Shared.Orchestration.Logging
{
    public class ActivityLogEntry
    {
        readonly DateTimeOffset occurred;
        readonly ActivityLogEntryCategory category;
        readonly ActorId actorId;
        readonly string message;
        readonly int? percentage;

        public ActivityLogEntry(
            DateTimeOffset occurred,
            ActivityLogEntryCategory category, 
            ActorId actorId, 
            string message,
            int? percentage)
        {
            this.occurred = occurred;
            this.category = category;
            this.actorId = actorId;
            this.message = message;
            this.percentage = percentage;
        }

        public ActorId ActorId
        {
            get { return actorId; }
        }

        public DateTimeOffset Occurred
        {
            get { return occurred; }
        }

        public ActivityLogEntryCategory Category
        {
            get { return category; }
        }

        public string Message
        {
            get { return message; }
        }

        public int? Percentage
        {
            get { return percentage; }
        }
    }
}