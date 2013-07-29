using System;
using Pipefish;

namespace Octopus.Shared.Communications.Logging
{
    public class ActivityLogEntry
    {
        readonly DateTimeOffset occurred;
        readonly ActivityLogCategory category;
        readonly ActorId actorId;
        readonly string message;

        public ActivityLogEntry(DateTimeOffset occurred, ActivityLogCategory category, ActorId actorId, string message)
        {
            this.occurred = occurred;
            this.category = category;
            this.actorId = actorId;
            this.message = message;
        }

        public ActorId ActorId
        {
            get { return actorId; }
        }

        public DateTimeOffset Occurred
        {
            get { return occurred; }
        }

        public ActivityLogCategory Category
        {
            get { return category; }
        }

        public string Message
        {
            get { return message; }
        }
    }
}