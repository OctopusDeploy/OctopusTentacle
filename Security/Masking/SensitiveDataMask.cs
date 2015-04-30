using System;
using System.Collections.Generic;
using System.Text;
// ReSharper disable ReplaceWithStringIsNullOrEmpty

namespace Octopus.Shared.Security.Masking
{
    // TODO: Rabin-Karp or something else might make this perform much better
    // http://en.wikipedia.org/wiki/Rabin%E2%80%93Karp_algorithm
    public class SensitiveDataMask
    {
        readonly List<string> valuesLongestToShortest = new List<string>();
        readonly List<string> valuesShortestToLongest = new List<string>();  
        const string Mask = "********";
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
            foreach (var instance in instancesToMask)
                MaskInstancesOf(instance);
        }

        public void MaskInstancesOf(string sensitive)
        {
            if (string.IsNullOrWhiteSpace(sensitive) || sensitive.Length < 4) 
                return;

            lock (sync)
            {
                valuesLongestToShortest.Add(sensitive);
                valuesShortestToLongest.Add(sensitive);

                valuesLongestToShortest.Sort(StringLengthComparer.LongestToShortest);
                valuesShortestToLongest.Sort(StringLengthComparer.ShortestToLongest);
            }
        }

        public string ApplyTo(string raw)
        {
            if (raw == null || raw.Length == 0 || raw.Length < 4 || valuesLongestToShortest.Count == 0)
                return raw;

            lock (sync)
            {
                if (!HasSensitiveValues(raw))
                    return raw;

                // It's only worth allocating the new string if we actually found a match
                builder.Clear();
                builder.EnsureCapacity(raw.Length);
                builder.Append(raw);
                foreach (var sensitive in valuesLongestToShortest)
                {
                    builder.Replace(sensitive, Mask);
                }

                return builder.ToString();
            }
        }

        bool HasSensitiveValues(string raw)
        {
            foreach (var sensitive in valuesShortestToLongest)
            {
                if (!raw.Contains(sensitive))
                {
                    continue;
                }
                return true;
            }
            return false;
        }

        class StringLengthComparer : IComparer<string>
        {
            readonly int multiplier;
            public static readonly StringLengthComparer LongestToShortest = new StringLengthComparer(-1);
            public static readonly StringLengthComparer ShortestToLongest = new StringLengthComparer(1);

            StringLengthComparer(int multiplier)
            {
                this.multiplier = multiplier;
            }

            public int Compare(string x, string y)
            {
                return (x.Length.CompareTo(y.Length)) * multiplier;
            }
        }
    }
}
