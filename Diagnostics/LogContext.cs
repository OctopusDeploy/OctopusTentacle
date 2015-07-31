using System;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Shared.Security.Masking;

namespace Octopus.Shared.Diagnostics
{
    public class LogContext
    {
        readonly string[] sensitiveValues;
        readonly string correlationId;
        readonly SensitiveDataMask sensitiveDataMask;

        [JsonConstructor]
        public LogContext(string correlationId = null, string[] sensitiveValues = null)
        {
            this.correlationId = correlationId ?? GenerateId();
            this.sensitiveValues = sensitiveValues ?? new string[0];
            if (this.sensitiveValues.Any())
            {
                sensitiveDataMask = new SensitiveDataMask();
                sensitiveDataMask.MaskInstancesOf(sensitiveValues);
            }
        }

        public string CorrelationId
        {
            get { return correlationId; }
        }

        public string SafeSanitize(string raw)
        {
            try
            {
                return sensitiveDataMask != null ? sensitiveDataMask.ApplyTo(raw) : raw;
            }
            catch
            {
                return raw;
            }
        }

        public LogContext CreateSibling()
        {
            return Parent().CreateChild();
        }

        public LogContext Parent()
        {
            var lastSlash = correlationId.LastIndexOf('/');
            if (lastSlash < 0)
                return new LogContext();

            return new LogContext(correlationId.Substring(0, lastSlash), sensitiveValues);
        }

        public LogContext CreateChild()
        {
            return new LogContext((correlationId + '/' + GenerateId()), sensitiveValues);
        }

        public LogContext WithSensitiveValues(string[] sensitiveValues)
        {
            return new LogContext(correlationId, this.sensitiveValues.Union(sensitiveValues).ToArray());
        }

        static string GenerateId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static LogContext Null()
        {
            var correlationId = GenerateId();
            return new LogContext(correlationId);
        }

        public static LogContext CreateNew(string correlationId)
        {
            if (correlationId == null) throw new ArgumentNullException("correlationId");
            return new LogContext(correlationId);
        }
    }
}