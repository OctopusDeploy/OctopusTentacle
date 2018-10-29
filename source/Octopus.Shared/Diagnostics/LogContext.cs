using System;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Shared.Model;
using Octopus.Shared.Security.Masking;

namespace Octopus.Shared.Diagnostics
{
    [DebuggerDisplay("{CorrelationId}")]
    public class LogContext
    {
        readonly string correlationId;
        readonly object sensitiveDataMaskLock = new object();
        string[] sensitiveValues;
        Lazy<AhoCorasick> trie;
        SensitiveDataMask sensitiveDataMask;

        [JsonConstructor]
        public LogContext(string correlationId = null, string[] sensitiveValues = null)
        {
            this.correlationId = correlationId ?? GenerateId();
            this.sensitiveValues = sensitiveValues ?? new string[0];
            trie = new Lazy<AhoCorasick>(CreateTrie);
        }

        private LogContext(string correlationId, string[] sensitiveValues, Lazy<AhoCorasick> trie)
        {
            this.correlationId = correlationId;
            this.sensitiveValues = sensitiveValues;
            this.trie = trie;
        }

        public string CorrelationId => correlationId;

        [Encrypted]
        [JsonIgnore]
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
                        }
                    }

                if (sensitiveDataMask != null)
                    sensitiveDataMask.ApplyTo(trie.Value, raw, action);
                else
                    action(raw);
            }
            catch
            {
                action(raw);
            }
        }

        public LogContext CreateChild(string[] sensitiveValues = null)
        {
            var id = correlationId + '/' + GenerateId();

            if (sensitiveValues == null || sensitiveValues.Length == 0)
            {
                // Reuse parent trie
                return new LogContext(id, this.sensitiveValues, trie);
            }

            return new LogContext(id, this.sensitiveValues.Union(sensitiveValues).ToArray());
        }

        /// <summary>
        /// Adds additional sensitive-variables to the LogContext. 
        /// </summary>
        /// <returns>The existing LogContext</returns>
        public LogContext WithSensitiveValues(string[] sensitiveValues)
        {
            if (sensitiveValues == null || sensitiveValues.Length == 0)
                return this;

            var initialSensitiveValuesCount = this.sensitiveValues.Length;
            var sensitiveValuesUnion = this.sensitiveValues.Union(sensitiveValues).ToArray();

            // If no new sensitive-values were added, avoid the cost of rebuilding the trie
            if (initialSensitiveValuesCount == sensitiveValuesUnion.Length)
                return this;

            // New sensitive-values were added, so reset.
            this.sensitiveValues = sensitiveValuesUnion;
            this.trie = new Lazy<AhoCorasick>(CreateTrie);
            return this;
        }

        /// <summary>
        /// Adds an additional sensitive-variable to the LogContext. 
        /// </summary>
        /// <returns>The existing LogContext</returns>
        public LogContext WithSensitiveValue(string sensitiveValue)
        {
            return WithSensitiveValues(new[] {sensitiveValue});
        }

        public void Flush()
        {
            sensitiveDataMask?.Flush(trie.Value);
        }

        static string GenerateId() => Guid.NewGuid().ToString("N");

        AhoCorasick CreateTrie()
        {
            if (sensitiveValues.Length == 0)
                return null;

            var trie = new AhoCorasick();
            foreach (var instance in sensitiveValues)
            {
                if (string.IsNullOrWhiteSpace(instance) || instance.Length < 4)
                    continue;

                var normalized = instance.Replace("\r\n", "").Replace("\n", "");

                trie.Add(normalized);
            }
            trie.Build();
            return trie;
        }
    }
}