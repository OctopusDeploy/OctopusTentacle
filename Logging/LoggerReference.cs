using System;
using Newtonsoft.Json;
using Octopus.Shared.Messages;
using Pipefish;
using Pipefish.Core;

namespace Octopus.Shared.Logging
{
    public class LoggerReference
    {
        private readonly string loggerActorId;
        private readonly string correlationId;
        
        [JsonConstructor]
        public LoggerReference(string loggerActorId, string correlationId = null)
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

        public LoggerReference CreateSibling()
        {
            return Parent().CreateChild();
        }

        public LoggerReference Parent()
        {
            var lastSlash = correlationId.LastIndexOf('/');
            if (lastSlash < 0)
                return new LoggerReference(loggerActorId);

            return new LoggerReference(loggerActorId, correlationId.Substring(0, lastSlash));
        }

        public LoggerReference CreateChild()
        {
            return new LoggerReference(loggerActorId, (correlationId + '/' + GenerateId()));
        }

        static string GenerateId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static LoggerReference Null(string spaceName)
        {
            if (spaceName == null) throw new ArgumentNullException("spaceName");
            var correlationId = GenerateId();
            return new LoggerReference(new ActorId(WellKnownActors.Anonymous, spaceName).ToString(), correlationId);
        }

        public static LoggerReference CreateNew(string spaceName, string correlationId)
        {
            if (spaceName == null) throw new ArgumentNullException("spaceName");
            if (correlationId == null) throw new ArgumentNullException("correlationId");
            return new LoggerReference(new ActorId(WellKnownOctopusActors.Logger, spaceName).ToString(), correlationId);
        }
    }
}