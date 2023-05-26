using System;
using System.Collections;
using System.Collections.Generic;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class V1OnlyAndV2ScriptServiceTentacleVersions : IEnumerable<string>
    {
        public IEnumerator<string> GetEnumerator()
        {
            yield return null; // Current code, will have v2
            yield return "6.3.451"; // v1 tentacle that does not have capabilities service.
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}