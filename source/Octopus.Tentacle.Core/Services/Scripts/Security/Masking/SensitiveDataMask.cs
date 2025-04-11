using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Octopus.Tentacle.Security.Masking
{
    public class SensitiveDataMask
    {
        public static readonly string Mask = "********";
        static readonly object sync = new object();
        readonly StringBuilder builder = new StringBuilder();
        readonly Queue<DeferredAction> deferred = new Queue<DeferredAction>();
        string? lastSearchPath;

        /// <summary>
        /// Masks instances of sensitive values and invokes the supplied action with the sanitized string.
        /// The reason this is implemented as a callback rather than directly returning the sanitized value is
        /// in the case of multi-line sensitive values.  If a raw string is detected as potential multi-line match,
        /// sanitizing and invoking the callback will be delayed until it is confirmed it is/isn't a match, or until
        /// Flush() is called.
        /// </summary>
        public void ApplyTo(AhoCorasick trie, string raw, Action<string> action)
        {
            lock (sync)
            {
                var found = trie.Find(raw, lastSearchPath ?? "");

                lastSearchPath = found.PartialPath;

                // If we are in a pending partial match then defer processing until we are sure it
                // is/isn't a match
                if (found.IsPartial)
                {
                    deferred.Enqueue(new DeferredAction(raw, action));
                    return;
                }

                // This result was not a partial match, so if there are any deferred actions,
                // add the current action to the end and process
                if (deferred.Any())
                {
                    deferred.Enqueue(new DeferredAction(raw, action));
                    ProcessDeferred(trie);
                    return;
                }

                // If there are no deferred actions and we found no matches, invoke the action
                // with the raw text
                if (!found.Found.Any())
                {
                    action(raw);
                    return;
                }

                // Mask the matches and perform the action
                MaskSectionsAndPerformAction(raw, action, GetMaskedSections(found.Found));
            }
        }

        void MaskSectionsAndPerformAction(string raw, Action<string> action, IEnumerable<Tuple<int, int>> maskedSections)
        {
            builder.Clear();
            builder.EnsureCapacity(raw.Length);

            var i = 0;

            foreach (var maskedSection in maskedSections)
            {
                // If we are not at the start of the masked section, then progress to it
                while (i < maskedSection.Item1)
                    builder.Append(raw[i++]);

                // Apply the mask
                builder.Append(Mask);
                i = maskedSection.Item2 + 1;
            }

            // Write any remaining raw text
            while (i < raw.Length)
                builder.Append(raw[i++]);

            action(builder.ToString());
        }

        /// <summary>
        /// Processes all deferred actions.
        /// Matches that span actions make this method tricky.
        /// We concatenate the original text of all deferred actions and search for matches within it.
        /// The possible scenarios then are:
        /// 1) There were no matches within a deferred action.  Easy, just invoke the action with the original text.
        /// 2) The action is completely spanned by a match.  In this case we want to discard the action. For example,
        ///    a private-key in PEM format may be logged across many lines.  We don't want to write a mask for each line.
        /// 3) There are one or more matches that start or end within the action.  In this case we need to mask the appropriate
        /// // sections.
        /// </summary>
        void ProcessDeferred(AhoCorasick trie)
        {
            // Concatenate all the deferred actions into one string and find matches in it
            builder.Clear();
            builder.EnsureCapacity(deferred.Sum(x => x.Text.Length));

            foreach (var text in deferred.Select(x => x.Text))
                builder.Append(text);

            var matches = trie.Find(builder.ToString());
            var maskedSections = new Queue<Tuple<int, int>>(GetMaskedSections(matches.Found));

            // Process each deferred action, masking as appropriate
            var currentEndIndex = -1;
            var currentMaskedSection = !maskedSections.Any() ? null : maskedSections.Dequeue();

            while (deferred.Any())
            {
                var current = deferred.Dequeue();
                var currentStartIndex = currentEndIndex + 1;
                currentEndIndex += current.Text.Length;

                // Scenario 1
                // If there are no sections remaining to be masked, or the current masked section begins
                // after this action, then simply invoke the action with the original text
                if (currentMaskedSection == null || currentMaskedSection.Item1 > currentEndIndex)
                {
                    current.Action(current.Text);
                    continue;
                }

                // Scenario 2
                // If the current masked section spans this entire action's text (i.e. this action was one line in a multi-line match)
                // then skip this action entirely. We don't want to write the mask value for every line of a multi-line match
                if (currentMaskedSection.Item1 < currentStartIndex && currentMaskedSection.Item2 >= currentEndIndex)
                    continue;

                // Scenario 3
                // This action has sections that need to be masked. We need to get the relative indexes for the masked sections
                var relativeIndexes = new List<Tuple<int, int>>();

                while (currentMaskedSection != null && currentMaskedSection.Item1 <= currentEndIndex)
                {
                    // The local start index will be either:
                    // 0 if the masked section begins before this action
                    // OR the relative index of the masked section
                    var localStartIndex = currentMaskedSection.Item1 <= currentStartIndex ? 0 : currentMaskedSection.Item1 - currentStartIndex;
                    // Likewise the local end index will be either:
                    // The end of this action's text if the masked section continues beyond this action
                    // OR the relative end index
                    var localEndIndex = currentMaskedSection.Item2 >= currentEndIndex ? current.Text.Length - 1 : currentMaskedSection.Item2 - currentStartIndex;
                    relativeIndexes.Add(Tuple.Create(localStartIndex, localEndIndex));

                    if (currentMaskedSection.Item2 > currentEndIndex)
                        break;

                    currentMaskedSection = maskedSections.Any() ? maskedSections.Dequeue() : null;
                }

                // We have our relative indexes, so mask the text and invoke the action
                MaskSectionsAndPerformAction(current.Text, current.Action, relativeIndexes);
            }
        }

        // Gets the start and end indexes of the masked sections
        // If the matches overlap, this will combine them
        static IEnumerable<Tuple<int, int>> GetMaskedSections(IEnumerable<KeyValuePair<int, string>> found)
        {
            var sections = new List<Tuple<int, int>>();

            foreach (var match in found.OrderBy(x => x.Key))
            {
                var matchStartIndex = match.Key - (match.Value.Length - 1);

                if (sections.Count > 0 && matchStartIndex < sections.Last().Item2)
                {
                    sections[sections.Count - 1] = Tuple.Create(sections.Last().Item1, match.Key);
                    continue;
                }

                sections.Add(Tuple.Create(matchStartIndex, match.Key));
            }

            return sections;
        }

        /// <summary>
        /// Process any deferred potential multi-line matches
        /// </summary>
        public void Flush(AhoCorasick trie)
        {
            lock (sync)
            {
                ProcessDeferred(trie);
            }
        }

        class DeferredAction
        {
            public DeferredAction(string text, Action<string> action)
            {
                Text = text;
                Action = action;
            }

            public string Text { get; }
            public Action<string> Action { get; }
        }
    }
}