using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Octopus.Shared.Security.Masking
{
    public class SensitiveDataMask
    {
        readonly ConcurrentDictionary<string, bool> sensitiveData = new ConcurrentDictionary<string, bool>();
        const string Mask = "********";

        public SensitiveDataMask()
        {
        }

        public SensitiveDataMask(IEnumerable<string> instancesToMask)
        {
            foreach (var instance in instancesToMask)
                MaskInstancesOf(instance);
        }

        public void MaskInstancesOf(string sensitive)
        {
            if (string.IsNullOrWhiteSpace(sensitive) || sensitive.Length < 4) return;
            sensitiveData.TryAdd(sensitive, true);
        }

        public string ApplyTo(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            foreach (var sensitive in sensitiveData.Keys.OrderByDescending(o => o.Length))
            {
                if (raw.Contains(sensitive))
                {
                    var result = new StringBuilder(raw);
                    
                    foreach (var sensitive2 in sensitiveData.Keys.OrderByDescending(o => o.Length))
                        result.Replace(sensitive2, Mask);

                    return result.ToString();
                }
            }

            return raw;
        }

        public Exception ApplyTo(Exception exception)
        {
            if (exception == null) return null;

            if (RequiresMask(exception))
                return new MaskedException(ApplyTo(exception.ToString()));

            return exception;
        }

        bool RequiresMask(Exception exception)
        {
            foreach (var sensitive in sensitiveData.Keys)
                if (exception.Message.Contains(sensitive))
                    return true;

            if (exception.InnerException != null &&
                RequiresMask(exception.InnerException))
                return true;

            var agg = exception as AggregateException;
            if (agg != null)
            {
                if (agg.InnerExceptions.Any(RequiresMask))
                    return true;
            }

            return false;
        }
    }
}
