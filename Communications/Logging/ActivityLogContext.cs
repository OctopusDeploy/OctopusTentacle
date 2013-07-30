using System;
using Newtonsoft.Json;
using Pipefish;

namespace Octopus.Shared.Communications.Logging
{
    public class ActivityLogContext
    {
        private readonly ActorId loggerActorId;
        private readonly string correlationId;
        
        [JsonConstructor]
        protected ActivityLogContext(ActorId loggerActorId, string correlationId)
        {
            this.loggerActorId = loggerActorId;
            this.correlationId = correlationId ?? GenerateId();
        }

        public ActorId LoggerActorId
        {
            get { return loggerActorId; }
        }

        public string CorrelationId
        {
            get { return correlationId; }
        }

        public ActivityLogContext CreateChild()
        {
            return new ActivityLogContext(loggerActorId, (correlationId + "/" + GenerateId()));
        }

        static string GenerateId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static ActivityLogContext CreateNew(string spaceName, string correlationId)
        {
            Guard.ArgumentNotNullOrEmpty(spaceName, "spaceName");
            Guard.ArgumentNotNullOrEmpty(correlationId, "correlationId");
            return new ActivityLogContext(new ActorId(WellKnownOctopusActors.Logger, spaceName), correlationId);
        }
    }
}