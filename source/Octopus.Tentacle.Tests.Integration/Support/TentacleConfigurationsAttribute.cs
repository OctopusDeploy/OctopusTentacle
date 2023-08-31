using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleConfigurationsAttribute : TestCaseSourceAttribute
    {
        public TentacleConfigurationsAttribute(bool testCommonVersions = true, bool testCapabilitiesServiceInterestingVersions = false)
        : base(
            typeof(TentacleConfigurationTestCases),
            nameof(TentacleConfigurationTestCases.GetEnumerator),
            new object[] { testCommonVersions , testCapabilitiesServiceInterestingVersions })
        {
        }
    }

    static class TentacleConfigurationTestCases
    {
        public static IEnumerator GetEnumerator(bool testCommonVersions, bool testCapabilitiesInterestingVersions)
        {
            var tentacleTypes = new[] { TentacleType.Listening, TentacleType.Polling };
            var halibutTypes = new[] { SyncOrAsyncHalibut.Sync, SyncOrAsyncHalibut.Async };
            List<Version?> versions = new List<Version?> { TentacleVersions.Current };

            if (testCommonVersions)
            {
                versions.AddRange(new []
                {
                    TentacleVersions.v5_0_15_LastOfVersion5,
                    TentacleVersions.v6_3_417_LastWithScriptServiceV1Only,
                    TentacleVersions.v7_0_1_ScriptServiceV2Added
                });
            }

            if (testCapabilitiesInterestingVersions)
            {
                versions.AddRange(new []
                {
                    TentacleVersions.v5_0_4_FirstLinuxRelease,
                    TentacleVersions.v5_0_12_AutofacServiceFactoryIsInShared,
                    TentacleVersions.v6_3_417_LastWithScriptServiceV1Only, // the autofac service is in tentacle, but tentacle does not have the capabilities service.
                    TentacleVersions.v7_0_1_ScriptServiceV2Added
                });
            }

            return (from tentacleType in tentacleTypes
                    from halibutType in halibutTypes
                    from version in versions.Distinct()
                    select new TentacleConfigurationTestCase(tentacleType, halibutType, version))
                .GetEnumerator();
        }
    }
}
