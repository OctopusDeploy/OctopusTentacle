using System;
using System.Collections;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleTypesAndCommonVersionsToTest : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new object[] { TentacleType.Polling, null };
            yield return new object[] { TentacleType.Polling, "5.0.15" };
            yield return new object[] { TentacleType.Polling, "6.3.417" };

            yield return new object[] { TentacleType.Listening, null };
            yield return new object[] { TentacleType.Listening, "5.0.15" };
            yield return new object[] { TentacleType.Listening, "6.3.417" };
        }
    }
}