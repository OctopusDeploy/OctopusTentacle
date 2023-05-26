using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class CartesianProduct
    {
        public static IEnumerable<IEnumerable<object>> Of(
            params IEnumerable[] sequences)
        {
            var accum = new List<object[]>();
            var list = sequences.ToList();
            if (list.Count > 0)
            {
                var enumStack = new Stack<IEnumerator>();
                var itemStack = new Stack<object>();
                int index = list.Count - 1;
                IEnumerator enumerator = list[index].GetEnumerator();
                while (true)
                    if (enumerator.MoveNext())
                    {
                        itemStack.Push(enumerator.Current);
                        if (index == 0)
                        {
                            accum.Add(itemStack.ToArray());
                            itemStack.Pop();
                        }
                        else
                        {
                            enumStack.Push(enumerator);
                            enumerator = list[--index].GetEnumerator();
                        }
                    }
                    else
                    {
                        if (++index == list.Count)
                            break;
                        itemStack.Pop();
                        enumerator = enumStack.Pop();
                    }
            }

            return accum;
        }
    }
}