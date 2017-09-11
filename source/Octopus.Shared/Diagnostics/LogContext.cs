using System;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Shared.Model;
using Octopus.Shared.Security.Masking;

namespace Octopus.Shared.Diagnostics
{
    public class LogContext
    {
        readonly string[] sensitiveValues;
        readonly string correlationId;
        SensitiveDataMask sensitiveDataMask;
        object sensitiveDataMaskLock = new object();

        [JsonConstructor]
        public LogContext(string correlationId = null, string[] sensitiveValues = null)
        {
            this.correlationId = correlationId ?? GenerateId();
            this.sensitiveValues = sensitiveValues ?? new string[0];
        }

        public string CorrelationId
        {
            get { return correlationId; }
        }

        [Encrypted]
        public string[] SensitiveValues
        {
            get { return sensitiveValues; }
        }

        public void SafeSanitize(string raw, Action<string> action)
        {
            try
            {
                lock (sensitiveDataMaskLock)
                {
                    if (sensitiveDataMask == null && sensitiveValues.Any())
                    {
                        sensitiveDataMask = new SensitiveDataMask();
                        sensitiveDataMask.MaskInstancesOf(sensitiveValues);
                    }
                }
                if (sensitiveDataMask != null)
                    sensitiveDataMask.ApplyTo(raw, action);
                else
                    action(raw);
            }
            catch
            {
                action(raw);
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

        public void Flush()
        {
            sensitiveDataMask?.Flush();
        }
    }
}