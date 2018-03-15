// Based on HashCodeCombiner from https://github.com/NuGet/NuGet.Client
// NuGet is licensed under the Apache license: https://github.com/NuGet/NuGet.Client/blob/dev/LICENSE.txt

using System;

namespace Octopus.Core.Util
{

    /// <summary>
    /// Hash code creator, based on the original NuGet hash code combiner/ASP hash code combiner implementations
    /// </summary>
    public sealed class HashCodeCombiner
    {
        // seed from String.GetHashCode()
        const long Seed = 0x1505L;

        long combinedHash;

        public HashCodeCombiner()
        {
            combinedHash = Seed;
        }

        public int CombinedHash
        {
            get { return combinedHash.GetHashCode(); }
        }

        public HashCodeCombiner AddInt32(int i)
        {
            combinedHash = ((combinedHash << 5) + combinedHash) ^ i;
            return this;
        }

        public HashCodeCombiner AddObject(int i)
        {
            AddInt32(i);
            return this;
        }

        public HashCodeCombiner AddObject(bool b)
        {
            AddInt32(b.GetHashCode());
            return this;
        }

        public HashCodeCombiner AddObject(object o)
        {
            if (o != null)
            {
                AddInt32(o.GetHashCode());
            }
            return this;
        }

        /// <summary>
        /// Create a unique hash code for the given set of items
        /// </summary>
        public static int GetHashCode(params object[] objects)
        {
            var combiner = new HashCodeCombiner();

            foreach (var obj in objects)
            {
                combiner.AddObject(obj);
            }

            return combiner.CombinedHash;
        }
    }
}