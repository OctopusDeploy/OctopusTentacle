using System;
using System.Linq;
using Octopus.Tentacle.Security.Masking;

namespace Octopus.Tentacle.Diagnostics
{
    public class SensitiveValueMasker
    {
        readonly object sensitiveDataMaskLock = new object();
        Lazy<AhoCorasick?> trie;
        SensitiveDataMask? sensitiveDataMask;

        public SensitiveValueMasker(string[]? sensitiveValues = null)
        {
            SensitiveValues = sensitiveValues ?? new string[0];
            trie = new Lazy<AhoCorasick?>(CreateTrie);
        }

        public string[] SensitiveValues { get; private set; }

        public void SafeSanitize(string raw, Action<string> action)
        {
            try
            {
                // JIT creation of sensitiveDataMask
                if (sensitiveDataMask == null && SensitiveValues.Length > 0)
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

        /// <summary>
        /// Adds additional sensitive-variables to the LogContext.
        /// </summary>
        /// <returns>The existing LogContext</returns>
        public SensitiveValueMasker WithSensitiveValues(string?[]? sensitiveValues)
        {
            if (sensitiveValues == null || sensitiveValues.Length == 0)
                return this;

            var initialSensitiveValuesCount = SensitiveValues.Length;
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
        public SensitiveValueMasker WithSensitiveValue(string sensitiveValue)
        {
            return WithSensitiveValues(new[] { sensitiveValue });
        }

        public void Flush()
        {
            if (trie.Value != null)
                sensitiveDataMask?.Flush(trie.Value);
        }

        AhoCorasick? CreateTrie()
        {
            if (SensitiveValues.Length == 0)
                return null;

            var t = new AhoCorasick();
            foreach (var instance in SensitiveValues)
            {
                if (string.IsNullOrWhiteSpace(instance) || instance.Length < 4)
                    continue;

                var normalized = instance.Replace("\r\n", "").Replace("\n", "");

                t.Add(normalized);
            }

            t.Build();
            return t;
        }
    }
}