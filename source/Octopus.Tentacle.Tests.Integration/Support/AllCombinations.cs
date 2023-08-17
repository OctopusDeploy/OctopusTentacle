using System;
using System.Collections;
using System.Collections.Generic;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class AllCombinations
    {
        private readonly List<IEnumerable> sequences = new ();

        public static AllCombinations Of(params object[] sequence)
        {
            return new AllCombinations().And(sequence);
        }

        public static AllCombinations Of(IEnumerable sequence)
        {
            return new AllCombinations().And(sequence);
        }

        public static AllCombinations AllValuesOf(Type enumType)
        {
            return new AllCombinations().AndAllValuesOf(enumType);
        }

        public AllCombinations And(IEnumerable sequence)
        {
            sequences.Add(sequence);
            return this;
        }

        public AllCombinations AndAllValuesOf(Type enumType)
        {

            return And(Enum.GetValues(enumType));
        }

        public AllCombinations And(params string?[] sequence)
        {
            sequences.Add(sequence);
            return this;
        }
        public AllCombinations And(params object?[] sequence)
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