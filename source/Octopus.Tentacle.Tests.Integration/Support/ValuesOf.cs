using System;
using System.Collections;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class ValuesOf
    {
        public static object[] CreateValues(Type sourceType)
        {
            var enumerable = ((IEnumerable) Activator.CreateInstance(sourceType));
            return enumerable.ToArrayOfObjects();
        }
    }
}