using System;
using Newtonsoft.Json;
using Pipefish.Core;

namespace Octopus.Shared.Communications.Logging
{
    public class LoggerReference
    {
        private readonly string loggerActorId;
        private readonly string correlationId;
        
        [JsonConstructor]
        public LoggerReference(string loggerActorId, string correlationId)
        {
            this.loggerActorId = loggerActorId;
            this.correlationId = correlationId ?? GenerateId();
        }

        public string LoggerActorId
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
            return new LoggerReference(new ActorId(WellKnownOctopusActors.Logger, spaceName).ToString(), correlationId);
        }
    }
}