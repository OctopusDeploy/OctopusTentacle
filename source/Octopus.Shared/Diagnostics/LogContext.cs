using System;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Shared.Security.Masking;

namespace Octopus.Shared.Diagnostics
{
    [DebuggerDisplay("{CorrelationId}")]
    public class LogContext : ILogContext
    {
        readonly object sensitiveDataMaskLock = new object();
        Lazy<AhoCorasick?> trie;
        SensitiveDataMask? sensitiveDataMask;

        [JsonConstructor]
        public LogContext(string? correlationId = null, string[]? sensitiveValues = null)
        {
            CorrelationId = correlationId ?? GenerateId();
            SensitiveValues = sensitiveValues ?? new string[0];
            trie = new Lazy<AhoCorasick?>(CreateTrie);
        }

        LogContext(string correlationId, string[] sensitiveValues, Lazy<AhoCorasick?> trie)
        {
            CorrelationId = correlationId;
            SensitiveValues = sensitiveValues;
            this.trie = trie;
        }

        public string CorrelationId { get; }

        public string[] SensitiveValues { get; set; }

        public void SafeSanitize(string raw, Action<string> action)
        {
            try
            {
                // JIT creation of sensitiveDataMask
                if (sensitiveDataMask == null && SensitiveValues != null && SensitiveValues.Length > 0)
                    lock (sensitiveDataMaskLock)
                    {
                        if (sensitiveDataMask == null && SensitiveValues.Length > 0)
                            sensitiveDataMask = new SensitiveDataMask();
                    }

                if (sensitiveDataMask != null && trie.Value != null)
                    sensitiveDataMask.ApplyTo(trie.Value, raw, action);
                else
                    action(raw);
            }
            catch
            {
                action(raw);
            }
        }

        public ILogContext CreateChild(string[]? sensitiveValues = null)
        {
            var id = CorrelationId + '/' + GenerateId();

            if (sensitiveValues == null || sensitiveValues.Length == 0)
                // Reuse parent trie
                return new LogContext(id, SensitiveValues, trie);

            return new LogContext(id, SensitiveValues.Union(sensitiveValues).ToArray());
        }

        /// <summary>
        /// Adds additional sensitive-variables to the LogContext.
        /// </summary>
        /// <returns>The existing LogContext</returns>
        public ILogContext WithSensitiveValues(string?[]? sensitiveValues)
        {
            if (sensitiveValues == null || sensitiveValues.Length == 0)
                return this;

            var initialSensitiveValuesCount = SensitiveValues?.Length ?? 0;
            // ReSharper disable once RedundantEnumerableCastCall
            var sensitiveValuesUnion = SensitiveValues.Union(sensitiveValues.Where(x => x != null).Cast<string>()).ToArray();

            // If no new sensitive-values were added, avoid the cost of rebuilding the trie
            if (initialSensitiveValuesCount == sensitiveValuesUnion.Length)
                return this;

            // New sensitive-values were added, so reset.
            SensitiveValues = sensitiveValuesUnion;
            trie = new Lazy<AhoCorasick?>(CreateTrie);
            return this;
        }

        /// <summary>
        /// Adds an additional sensitive-variable to the LogContext.
        /// </summary>
        /// <returns>The existing LogContext</returns>
        public ILogContext WithSensitiveValue(string sensitiveValue)
        {
            return WithSensitiveValues(new[] { sensitiveValue });
        }

        public void Flush()
        {
            if (trie.Value != null)
                sensitiveDataMask?.Flush(trie.Value);
        }

        static string GenerateId() => Guid.NewGuid().ToString("N");

        AhoCorasick? CreateTrie()
        {
            if (SensitiveValues == null || SensitiveValues.Length == 0)
                return null;

            var trie = new AhoCorasick();
            foreach (var instance in SensitiveValues)
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