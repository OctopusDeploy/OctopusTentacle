using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Org.BouncyCastle.Crypto.Modes;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class AllCombinations
    {
        private List<IEnumerable> sequences = new ();
        
        public static AllCombinations Of(IEnumerable sequence)
        {
            return new AllCombinations().And(sequence);
        }

        public AllCombinations And(IEnumerable sequence)
        {
            sequences.Add(sequence);
            return this;
        }

        public AllCombinations And(params string[] sequence)
        {
            sequences.Add(sequence);
            return this;
        }
        public AllCombinations And(params object[] sequence)
        {
            sequences.Add(sequence);
            return this;
        }

        public IEnumerator Build()
        {
            return CartesianProduct.Of(sequences.ToArray()).GetEnumerator();
        }
    }
}