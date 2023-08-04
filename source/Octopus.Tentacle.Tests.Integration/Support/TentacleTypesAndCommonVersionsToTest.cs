using System;
using System.Collections;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleTypesAndCommonVersionsToTest : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new object[] { TentacleType.Polling, TentacleVersions.Current };
            yield return new object[] { TentacleType.Polling, TentacleVersions.v5_0_15_LastOfVersion5 };
            yield return new object[] { TentacleType.Polling, TentacleVersions.v6_3_417_LastWithScriptServiceV1Only };

            yield return new object[] { TentacleType.Listening, TentacleVersions.Current };
            yield return new object[] { TentacleType.Listening, TentacleVersions.v5_0_15_LastOfVersion5 };
            yield return new object[] { TentacleType.Listening, TentacleVersions.v6_3_417_LastWithScriptServiceV1Only };
        }
    }
}
