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
        readonly LogContext parent;
        readonly object sensitiveDataMaskLock = new object();
        SensitiveDataMask sensitiveDataMask;

        [JsonConstructor]
        public LogContext(string correlationId = null, string[] sensitiveValues = null, LogContext parent = null)
        {
            this.correlationId = correlationId ?? GenerateId();
            this.sensitiveValues = sensitiveValues ?? new string[0];
            this.parent = parent ?? this;
        }

        public string CorrelationId => correlationId;

        [Encrypted]
        public string[] SensitiveValues => sensitiveValues;

        public void SafeSanitize(string raw, Action<string> action)
        {
            try
            {
                // JIT creation of sensitiveDataMask
                if (sensitiveDataMask == null && sensitiveValues.Length > 0)
                    lock (sensitiveDataMaskLock)
                    {
                        if (sensitiveDataMask == null && sensitiveValues.Length > 0)
                        {
                            sensitiveDataMask = new SensitiveDataMask();
                            sensitiveDataMask.MaskInstancesOf(sensitiveValues);
                        }
                    }

                // Chain action with parents SafeSanitize
                Action<string> actionWithParent = s =>
                {
                    if (parent == this)
                        action(s);
                    else
                        parent.SafeSanitize(s, action);
                };

                if (sensitiveDataMask != null)
                    sensitiveDataMask.ApplyTo(raw, actionWithParent);
                else
                    actionWithParent(raw);
            }
            catch
            {
                action(raw);
            }
        }

        public LogContext CreateSibling() => Parent().CreateChild();

        public LogContext Parent() => parent;

        public LogContext CreateChild() => new LogContext((correlationId + '/' + GenerateId()), parent: this);

        public LogContext WithSensitiveValues(string[] sensitiveValues)
            => new LogContext(correlationId, this.sensitiveValues.Union(sensitiveValues).ToArray(), parent: parent);

        static string GenerateId() => Guid.NewGuid().ToString("N");

        public static LogContext Null() => new LogContext(GenerateId());

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