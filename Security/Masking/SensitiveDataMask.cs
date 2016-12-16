using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Octopus.Shared.Security.Masking
{
    // TODO: Rabin-Karp or something else might make this perform much better
    // http://en.wikipedia.org/wiki/Rabin%E2%80%93Karp_algorithm
    public class SensitiveDataMask
    {
        public static readonly string Mask = "********";
        readonly List<string> valuesLongestToShortest = new List<string>();
        readonly List<string> valuesShortestToLongest = new List<string>();
        readonly List<string[]> multiLineValues = new List<string[]>();
        readonly List<PotentialMultiLineMatch> potentialMultiLineMatches = new List<PotentialMultiLineMatch>();
        readonly Queue<KeyValuePair<string, Action<string>>> pendingActions = new Queue<KeyValuePair<string, Action<string>>>();   
        readonly StringBuilder builder = new StringBuilder();
        readonly object sync = new object();

        public SensitiveDataMask()
        {
        }

        public SensitiveDataMask(IEnumerable<string> instancesToMask)
        {
            MaskInstancesOf(instancesToMask);
        }

        public void MaskInstancesOf(IEnumerable<string> instancesToMask)
        {
            if (instancesToMask == null) return;
            foreach (var instance in instancesToMask)
                MaskInstancesOf(instance);
        }

        public void MaskInstancesOf(string sensitive)
        {
            if (string.IsNullOrWhiteSpace(sensitive) || sensitive.Length < 4)
                return;

            var lines = sensitive.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);

            lock (sync)
            {
                if (lines.Length > 1)
                {
                    multiLineValues.Add(lines);
                }
                else
                {
                    valuesLongestToShortest.Add(sensitive);
                    valuesShortestToLongest.Add(sensitive);

                    valuesLongestToShortest.Sort(StringLengthComparer.LongestToShortest);
                    valuesShortestToLongest.Sort(StringLengthComparer.ShortestToLongest);
                }
            }
        }

        /// <summary>
        /// Masks instances of sensitive values and invokes the supplied action with the sanitized string. 
        /// The reason this is implemented as a callback rather than directly returning the sanitized value is
        /// in the case of multi-line sensitive values.  If a raw string is detected as potential multi-line match,
        /// sanitizing and invoking the callback will be delayed until it is confirmed it is/isn't a match, or until 
        /// Flush() is called.
        /// </summary>
        public void ApplyTo(string raw, Action<string> action)
        {
            lock (sync)
            {
                if (!ApplyMultiLineValuesTo(raw, action))
                {
                    ApplySingleLineValuesTo(raw, action);
                }
            }
        }

        /// <summary>
        /// Process any queued potential multi-line matches 
        /// </summary>
        public void Flush()
        {
            lock (sync)
            {
                while (pendingActions.Any())
                {
                    var pending = pendingActions.Dequeue();
                    ApplySingleLineValuesTo(pending.Key, pending.Value);
                }
            }
        }

        void ApplySingleLineValuesTo(string raw, Action<string> action)
        {
            if (string.IsNullOrEmpty(raw) || raw.Length < 4 || valuesLongestToShortest.Count == 0 
                || !ContainsSensitiveValues(raw))
            {
                action(raw);
                return;
            }

            // It's only worth allocating the new string if we actually found a match
            action(PerformMask(raw, valuesLongestToShortest));
        }

        string PerformMask(string raw, IList<string> values)
        {
            builder.Clear();
            builder.EnsureCapacity(raw.Length);
            builder.Append(raw);
            foreach (var sensitive in values)
            {
                builder.Replace(sensitive, Mask);
            }

            return builder.ToString();
        }

        bool ApplyMultiLineValuesTo(string raw, Action<string> action)
        {
            var actionQueued = false; 

            // If there are existing partial matches 
            if (potentialMultiLineMatches.Any())
            {
                var rejectedPotentialMatches = new List<PotentialMultiLineMatch>();

                foreach (var potentialMatch in potentialMultiLineMatches)
                {
                    // If the next line is still a potential match
                    if (raw.Contains(potentialMatch.NextLine))
                    {
                        // Is it the final line 
                        if (potentialMatch.Line == potentialMatch.MultilineValue.Length - 1)
                        {
                            // We have matched a multi-line value.  Rather than masking every-line, we will mask only the first line
                            action(PerformMask(potentialMatch.FirstMatch, 
                                valuesLongestToShortest.Concat(new[] {potentialMatch.MultilineValue.First()}).OrderByDescending(s => s.Length).ToList()));

                            // At this point we will clear any other potential matches.
                            // This certainly leaves some edge cases where multi-line values overlap, 
                            // but they are not what this was designed to deal with.
                            potentialMultiLineMatches.Clear();
                            pendingActions.Clear();
                            return true;
                        }

                        potentialMatch.AddMatch(raw);

                        if (!actionQueued)
                        {
                            pendingActions.Enqueue(new KeyValuePair<string, Action<string>>(raw, action));
                            actionQueued = true;
                        }
                    }
                    else
                    {
                        // The multi-line match has missed, so we will remove it from the potential matches
                        rejectedPotentialMatches.Add(potentialMatch);
                    }
                }

                // Remove all rejected matches
                foreach (var miss in rejectedPotentialMatches)
                    potentialMultiLineMatches.Remove(miss);

                // Process any actions
                while (pendingActions.Any() && potentialMultiLineMatches.All(potentialMatch => !potentialMatch.Matches.Contains(pendingActions.Peek().Key)))
                {
                    var pendingAction = pendingActions.Dequeue();
                    ApplySingleLineValuesTo(pendingAction.Key, pendingAction.Value);
                }
            }

            // Check if the first line of any multi-line values match  
            foreach (var multiLineValue in multiLineValues)
            {
                if (raw.Contains(multiLineValue.First()))
                {
                    var potentialMatch = new PotentialMultiLineMatch(multiLineValue, raw);
                    potentialMultiLineMatches.Add(potentialMatch);
                    potentialMultiLineMatches.Sort((x, y) => y.MultilineValue.Length.CompareTo(x.MultilineValue.Length));

                    if (!actionQueued)
                    {
                        pendingActions.Enqueue(new KeyValuePair<string, Action<string>>(raw, action));
                        actionQueued = true;
                    }
                }
            }

            return actionQueued;
        }

        bool ContainsSensitiveValues(string raw)
        {
            foreach (var sensitive in valuesShortestToLongest)
            {
                if (raw.Contains(sensitive))
                {
                    return true;
                }
            }

            return false;
        }

        class StringLengthComparer : IComparer<string>
        {
            public static readonly StringLengthComparer LongestToShortest = new StringLengthComparer(-1);
            public static readonly StringLengthComparer ShortestToLongest = new StringLengthComparer(1);
            readonly int multiplier;

            StringLengthComparer(int multiplier)
            {
                this.multiplier = multiplier;
            }

            public int Compare(string x, string y)
            {
                return (x.Length.CompareTo(y.Length))*multiplier;
            }
        }

        class PotentialMultiLineMatch
        {
            public PotentialMultiLineMatch(string[] multilineValue, string initialMatch)
            {
                Line = 1;
                MultilineValue = multilineValue;
                Matches = new HashSet<string> {initialMatch};
                FirstMatch = initialMatch;
            } 

            public int Line { get; private set;  }
            public string[] MultilineValue { get; }
            public string NextLine => MultilineValue[Line];
            public string FirstMatch { get; }
            public HashSet<string> Matches { get; }

            public void AddMatch(string match)
            {
                Line++;
                Matches.Add(match);
            }


        }
    }
}