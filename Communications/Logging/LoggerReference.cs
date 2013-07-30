using System;
using Newtonsoft.Json;
using Pipefish;

namespace Octopus.Shared.Communications.Logging
{
    public class LoggerReference
    {
        private readonly ActorId loggerActorId;
        private readonly string correlationId;
        
        [JsonConstructor]
        protected LoggerReference(ActorId loggerActorId, string correlationId)
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

        public LoggerReference CreateChild()
        {
            return new LoggerReference(loggerActorId, (correlationId + "/" + GenerateId()));
        }

        static string GenerateId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static LoggerReference CreateNew(string spaceName, string correlationId)
        {
            Guard.ArgumentNotNullOrEmpty(spaceName, "spaceName");
            Guard.ArgumentNotNullOrEmpty(correlationId, "correlationId");
            return new LoggerReference(new ActorId(WellKnownOctopusActors.Logger, spaceName), correlationId);
        }
    }
}