using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleConfigurationsAttribute : TentacleTestCaseSourceAttribute
    {
        public TentacleConfigurationsAttribute(
            bool testCommonVersions = false,
            bool testCapabilitiesServiceVersions = false,
            bool testNoCapabilitiesServiceVersions = false,
            bool testScriptIsolationLevelVersions = false,
            bool testDefaultTentacleRuntimeOnly = false,
            params object[] additionalParameterTypes)
            : base(
                typeof(TentacleConfigurationTestCases),
                nameof(TentacleConfigurationTestCases.GetEnumerator),
                new object[] { testCommonVersions, testCapabilitiesServiceVersions, testNoCapabilitiesServiceVersions, testScriptIsolationLevelVersions, testDefaultTentacleRuntimeOnly, additionalParameterTypes })
        {
        }
    }

    static class TentacleConfigurationTestCases
    {
        static readonly Type ScriptServiceV3AlphaType =  typeof(IAsyncClientScriptServiceV3Alpha);
        static readonly Type ScriptServiceV2Type =  typeof(IAsyncClientScriptServiceV2);
        static readonly Type ScriptServiceV1Type =  typeof(IAsyncClientScriptService);

        static readonly Type CurrentScriptServiceTypes = ScriptServiceV3AlphaType;

        static readonly Dictionary<Version, Type> ScriptServiceVersionTypesMap = new()
        {
            [TentacleVersions.v5_0_4_FirstLinuxRelease] = ScriptServiceV1Type,
            [TentacleVersions.v5_0_12_AutofacServiceFactoryIsInShared] = ScriptServiceV1Type,
            [TentacleVersions.v5_0_15_LastOfVersion5] = ScriptServiceV1Type,
            [TentacleVersions.v6_3_417_LastWithScriptServiceV1Only] = ScriptServiceV1Type,
            [TentacleVersions.v6_3_451_NoCapabilitiesService] = ScriptServiceV1Type,
            [TentacleVersions.v7_0_1_ScriptServiceV2Added] = ScriptServiceV2Type,
            [TentacleVersions.v8_0_34_LastWithoutScriptServiceV3Alpha] = ScriptServiceV2Type
        };

        public static IEnumerator GetEnumerator(
            bool testCommonVersions,
            bool testCapabilitiesVersions,
            bool testNoCapabilitiesServiceVersions,
            bool testScriptIsolationLevel,
            bool testDefaultTentacleRuntimeOnly,
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
                    TentacleVersions.v8_0_34_LastWithoutScriptServiceV3Alpha
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
                    TentacleVersions.v8_0_34_LastWithoutScriptServiceV3Alpha
                });
            }

            if (testScriptIsolationLevel)
            {
                versions.AddRange(new[]
                {
                    TentacleVersions.Current,
                    TentacleVersions.v6_3_417_LastWithScriptServiceV1Only,
                    TentacleVersions.v8_0_34_LastWithoutScriptServiceV3Alpha // Testing against v1 and v2 script services
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
                select new TentacleConfigurationTestCase(
                    tentacleType,
                    runtime,
                    version,
                    //null == current version and you can't have a null dictionary key
                    version != null ?
                        ScriptServiceVersionTypesMap[version]
                        : CurrentScriptServiceTypes);

            if (additionalParameterTypes.Length == 0)
            {
                return testCases.GetEnumerator();
            }

            return CombineTestCasesWithAdditionalParameters(testCases, additionalParameterTypes);
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