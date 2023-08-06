using System;
using System.Collections;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleTypesAndCommonVersionsToTest : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            return AllCombinations
                .Of(TentacleType.Polling,
                    TentacleType.Listening)
                .And(
                    TentacleVersions.Current,
                    TentacleVersions.v5_0_15_LastOfVersion5,
                    TentacleVersions.v6_3_417_LastWithScriptServiceV1Only,
                    TentacleVersions.v7_0_1_ScriptServiceV2Added
                )
                .Build();
        }
    }
}
