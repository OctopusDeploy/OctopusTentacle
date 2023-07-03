using System.Collections;
using System.Collections.Generic;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleTypesToTest : IEnumerable<TentacleType>
    {
        public IEnumerator<TentacleType> GetEnumerator()
        {
            yield return TentacleType.Polling;
            yield return TentacleType.Listening;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
