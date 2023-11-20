using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleConfigurationsAttribute : TentacleTestCaseSourceAttribute
    {
        public TentacleConfigurationsAttribute(bool testCommonVersions = false,
            bool testCapabilitiesServiceVersions = false,
            bool testNoCapabilitiesServiceVersions = false,
            bool testScriptIsolationLevelVersions = false,
            bool testDefaultTentacleRuntimeOnly = false,
            ScriptServiceVersionToTest scriptServiceToTest = ScriptServiceVersionToTest.None,
            params object[] additionalParameterTypes)
            : base(
                typeof(TentacleConfigurationTestCases),
                nameof(TentacleConfigurationTestCases.GetEnumerator),
                new object[]
                {
                    testCommonVersions, testCapabilitiesServiceVersions, testNoCapabilitiesServiceVersions, testScriptIsolationLevelVersions, testDefaultTentacleRuntimeOnly, scriptServiceToTest, additionalParameterTypes
                })
        {
        }
    }

    static class TentacleConfigurationTestCases
    {
        public static readonly Type ScriptServiceV3AlphaType = typeof(IAsyncClientScriptServiceV3Alpha);
        public static readonly Type ScriptServiceV2Type = typeof(IAsyncClientScriptServiceV2);
        public static readonly Type ScriptServiceV1Type = typeof(IAsyncClientScriptService);

        static readonly IEnumerable<Type> CurrentScriptServiceVersionsToTest = new[] { ScriptServiceV2Type, ScriptServiceV3AlphaType };

        static readonly Dictionary<Version, IEnumerable<Type>> ScriptServiceVersionsToTestMap = new()
        {
            [TentacleVersions.v5_0_4_FirstLinuxRelease] = new[] { ScriptServiceV1Type },
            [TentacleVersions.v5_0_12_AutofacServiceFactoryIsInShared] = new[] { ScriptServiceV1Type },
            [TentacleVersions.v5_0_15_LastOfVersion5] = new[] { ScriptServiceV1Type },
            [TentacleVersions.v6_3_417_LastWithScriptServiceV1Only] = new[] { ScriptServiceV1Type },
            [TentacleVersions.v6_3_451_NoCapabilitiesService] = new[] { ScriptServiceV1Type },
            [TentacleVersions.v7_1_189_SyncHalibutAndScriptServiceV2] = new[] { ScriptServiceV2Type },
            [TentacleVersions.v8_0_81_AsyncHalibutAndLastWithoutScriptServiceV3Alpha] = new[] { ScriptServiceV2Type }
        };

        public static IEnumerator GetEnumerator(
            bool testCommonVersions,
            bool testCapabilitiesVersions,
            bool testNoCapabilitiesServiceVersions,
            bool testScriptIsolationLevel,
            bool testDefaultTentacleRuntimeOnly,
            ScriptServiceVersionToTest scriptServiceToTest,
            object[] additionalParameterTypes)
        {
            var tentacleTypes = new[] { TentacleType.Listening, TentacleType.Polling };
            List<Version?> versions = new();

            if (testCommonVersions)
            {
                versions.AddRange(new[]
                {
                    TentacleVersions.Current,
                    TentacleVersions.v5_0_15_LastOfVersion5,
                    TentacleVersions.v6_3_417_LastWithScriptServiceV1Only,
                    TentacleVersions.v7_1_189_SyncHalibutAndScriptServiceV2,
                    TentacleVersions.v8_0_81_AsyncHalibutAndLastWithoutScriptServiceV3Alpha
                });
            }

            if (testCapabilitiesVersions)
            {
                versions.AddRange(new[]
                {
                    TentacleVersions.Current,
                    TentacleVersions.v5_0_4_FirstLinuxRelease,
                    TentacleVersions.v5_0_12_AutofacServiceFactoryIsInShared,
                    TentacleVersions.v6_3_417_LastWithScriptServiceV1Only, // the autofac service is in tentacle, but tentacle does not have the capabilities service.
                    TentacleVersions.v7_1_189_SyncHalibutAndScriptServiceV2,
                    TentacleVersions.v8_0_81_AsyncHalibutAndLastWithoutScriptServiceV3Alpha
                });
            }

            if (testScriptIsolationLevel)
            {
                versions.AddRange(new[]
                {
                    TentacleVersions.Current,
                    TentacleVersions.v6_3_417_LastWithScriptServiceV1Only,
                    TentacleVersions.v7_1_189_SyncHalibutAndScriptServiceV2, // Testing against v1 and v2 script services
                    TentacleVersions.v8_0_81_AsyncHalibutAndLastWithoutScriptServiceV3Alpha // Testing against v1 and v2 script services
                });
            }

            if (testNoCapabilitiesServiceVersions)
            {
                versions.AddRange(new[]
                {
                    TentacleVersions.v6_3_451_NoCapabilitiesService
                });
            }

            if (versions.Count == 0)
            {
                versions.Add(TentacleVersions.Current);
            }

            var runtimes = new List<TentacleRuntime> { DefaultTentacleRuntime.Value };
#if !NETFRAMEWORK
            if (!testDefaultTentacleRuntimeOnly && PlatformDetection.IsRunningOnWindows)
            {
                runtimes = new List<TentacleRuntime> { TentacleRuntime.DotNet6, TentacleRuntime.Framework48 };
            }
#endif

            var testCases =
                from tentacleType in tentacleTypes
                from runtime in runtimes
                from version in versions.Distinct()
                from serviceToTest in GetScriptServicesToTest(scriptServiceToTest, version)
                select new TentacleConfigurationTestCase(
                    tentacleType,
                    runtime,
                    version,
                    serviceToTest);

            if (additionalParameterTypes.Length == 0)
            {
                return testCases.GetEnumerator();
            }

            return CombineTestCasesWithAdditionalParameters(testCases, additionalParameterTypes);
        }

        static IEnumerable<Type> GetScriptServicesToTest(ScriptServiceVersionToTest scriptServiceToTest, Version? version)
        {
            return scriptServiceToTest switch
            {
                ScriptServiceVersionToTest.Version1 => new[] { ScriptServiceV1Type },
                ScriptServiceVersionToTest.Version2 => new[] { ScriptServiceV2Type },
                ScriptServiceVersionToTest.Version3Alpha => new[] { ScriptServiceV3AlphaType },
                //if no specific script service version was specified, fallback on the services in the tentacle version
                ScriptServiceVersionToTest.None => version != null
                    ? ScriptServiceVersionsToTestMap[version]
                    : CurrentScriptServiceVersionsToTest,

                _ => throw new ArgumentOutOfRangeException(nameof(scriptServiceToTest), scriptServiceToTest, null)
            };
        }

        static IEnumerator CombineTestCasesWithAdditionalParameters(IEnumerable<TentacleConfigurationTestCase> testCases, object[] additionalEnumerables)
        {
            AllCombinations enums = new AllCombinations();
            var additionalEnums = additionalEnumerables.Cast<Type>().ToArray();

            for (int i = 0; i < additionalEnums.Length; i++)
            {
                var additionalEnum = additionalEnums[i];
                object[] values;
                if (additionalEnum.IsEnum)
                {
                    values = Enum.GetValues(additionalEnums[i]).ToArrayOfObjects();
                }
                else if (typeof(IEnumerable).IsAssignableFrom(additionalEnum))
                {
                    values = ValuesOf.CreateValues(additionalEnum);
                }
                else
                {
                    throw new ArgumentException($"Enumerable type must be either an enum or implement IEnumerable: '{additionalEnum}'");
                }

                if (values.Length == 0)
                {
                    throw new ArgumentException($"Enumerable type does not contain any values: '{additionalEnum}'");
                }

                enums.And(values);
            }

            var enumerablePermutations = enums.BuildEnumerable();

            return (from testCase in testCases
                    from IEnumerable<object> permutation in enumerablePermutations
                    select CombineTestCaseWithAdditionalParameters(testCase, permutation.ToArray()))
                .GetEnumerator();
        }

        static object[] CombineTestCaseWithAdditionalParameters(TentacleConfigurationTestCase testCase, object[] additionalParameters)
        {
            var parameters = new object[1 + additionalParameters.Length];
            parameters[0] = testCase;
            Array.Copy(additionalParameters, 0, parameters, 1, additionalParameters.Length);
            return parameters;
        }
    }
}