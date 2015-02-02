using System;
using Newtonsoft.Json;

namespace Octopus.Shared.Logging
{
    public class LogCorrelator
    {
        private readonly string correlationId;
        
        [JsonConstructor]
        public LogCorrelator(string correlationId = null)
        {
            this.correlationId = correlationId ?? GenerateId();
        }

        public string CorrelationId
        {
            get { return correlationId; }
        }

        public LogCorrelator CreateSibling()
        {
            return Parent().CreateChild();
        }

        public LogCorrelator Parent()
        {
            var lastSlash = correlationId.LastIndexOf('/');
            if (lastSlash < 0)
                return new LogCorrelator();

            return new LogCorrelator(correlationId.Substring(0, lastSlash));
        }

        public LogCorrelator CreateChild()
        {
            return new LogCorrelator((correlationId + '/' + GenerateId()));
        }

        static string GenerateId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static LogCorrelator Null()
        {
            var correlationId = GenerateId();
            return new LogCorrelator(correlationId);
        }

        public static LogCorrelator CreateNew(string correlationId)
        {
            if (correlationId == null) throw new ArgumentNullException("correlationId");
            return new LogCorrelator(correlationId);
        }
    }
}