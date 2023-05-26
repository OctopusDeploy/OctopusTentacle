using System;
using System.Collections;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class AllTentacleTypesWithV1V2ScriptServices : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            return CartesianProduct.Of(new TentacleTypesToTest(), new V1OnlyAndV2ScriptServiceTentacleVersions()).GetEnumerator();
        }
    }
}