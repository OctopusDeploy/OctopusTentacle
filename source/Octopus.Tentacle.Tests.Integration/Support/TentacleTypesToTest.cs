using System.Collections;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleTypesToTest : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new object[] { TentacleType.Polling };
            yield return new object[] { TentacleType.Listening };
        }
    }
}