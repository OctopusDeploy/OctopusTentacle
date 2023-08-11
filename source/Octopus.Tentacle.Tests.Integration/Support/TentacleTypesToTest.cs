using System.Collections;
using System.Collections.Generic;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleTypesToTest : IEnumerable
    {
        IEnumerator IEnumerable.GetEnumerator()
        {
            return AllCombinations
                .Of(TentacleType.Polling,
                    TentacleType.Listening)
                .And(
                    SyncOrAsyncHalibut.Sync,
                    SyncOrAsyncHalibut.Async
                )
                .Build();
        }
    }
}
