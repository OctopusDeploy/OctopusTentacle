using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleConfigurationsAttribute : TestCaseSourceAttribute
    {
        public TentacleConfigurationsAttribute(
            bool testCommonVersions = false,
            bool testCapabilitiesServiceInterestingVersions = false,
            bool testStopPortForwarderAfterFirstCall = false,
            bool testRpcCalls = false,
            bool testRpcCallStages = false,
            bool testScriptIsolationLevel = false,
            bool testScriptsInParallel = false,
            params object[] additionalParameterTypes)
            : base(
                typeof(TentacleConfigurationTestCases),
                nameof(TentacleConfigurationTestCases.GetEnumerator),
                new object[] {testCommonVersions, testCapabilitiesServiceInterestingVersions, testStopPortForwarderAfterFirstCall, testRpcCalls, testRpcCallStages, testScriptIsolationLevel, testScriptsInParallel, additionalParameterTypes})
        {
        }
    }

    static class TentacleConfigurationTestCases
    {
        public static IEnumerator GetEnumerator(
            bool testCommonVersions,
            bool testCapabilitiesInterestingVersions,
            bool testStopPortForwarderAfterFirstCall,
            bool testRpcCalls,
            bool testRpcCallStages,
            bool testScriptIsolationLevel,
            bool testScriptsInParallel,
            object[] additionalParameterTypes)
        {
            var tentacleTypes = new[] {TentacleType.Listening, TentacleType.Polling};
            var halibutTypes = new[] {SyncOrAsyncHalibut.Sync, SyncOrAsyncHalibut.Async};
            List<Version?> versions = new List<Version?> {TentacleVersions.Current};

            if (testCommonVersions)
            {
                versions.AddRange(new[]
                {
                    TentacleVersions.v5_0_15_LastOfVersion5,
                    TentacleVersions.v6_3_417_LastWithScriptServiceV1Only,
                    TentacleVersions.v7_0_1_ScriptServiceV2Added
                });
            }

            if (testCapabilitiesInterestingVersions)
            {
                versions.AddRange(new[]
                {
                    TentacleVersions.v5_0_4_FirstLinuxRelease,
                    TentacleVersions.v5_0_12_AutofacServiceFactoryIsInShared,
                    TentacleVersions.v6_3_417_LastWithScriptServiceV1Only, // the autofac service is in tentacle, but tentacle does not have the capabilities service.
                    TentacleVersions.v7_0_1_ScriptServiceV2Added
                });
            }

            if (testScriptIsolationLevel || testScriptsInParallel)
            {
                versions.AddRange(new[]
                {
                    TentacleVersions.v6_3_417_LastWithScriptServiceV1Only,
                    TentacleVersions.v7_0_1_ScriptServiceV2Added // Testing against v1 and v2 script services
                });
            }

            List<bool?> stopPortForwarderAfterFirstCallValues =
                testStopPortForwarderAfterFirstCall
                    ? new List<bool?> {true, false}
                    : new List<bool?> {null};

            List<RpcCall?> rpcCalls = testRpcCalls
                ? new List<RpcCall?> {RpcCall.FirstCall, RpcCall.RetryingCall}
                : new List<RpcCall?> {null};

            List<RpcCallStage?> rpcCallStages = testRpcCallStages
                ? new List<RpcCallStage?> {RpcCallStage.Connecting, RpcCallStage.InFlight}
                : new List<RpcCallStage?> {null};

            List<ScriptIsolationLevel?> scriptIsolationLevels = testScriptIsolationLevel
                ? new List<ScriptIsolationLevel?> {ScriptIsolationLevel.FullIsolation, ScriptIsolationLevel.NoIsolation}
                : new List<ScriptIsolationLevel?> {null};

            List<ScriptsInParallelTestCase?> scriptsInParallelTestCases = testScriptsInParallel
                ? new List<ScriptsInParallelTestCase?>
                {
                    // Scripts with different mutex names can run at the same time.
                    ScriptsInParallelTestCase.FullIsolationDifferentMutex,
                    // Scripts with the same mutex name can run at the same time if they both has no isolation.
                    ScriptsInParallelTestCase.NoIsolationSameMutex
                }
                : new List<ScriptsInParallelTestCase?> {null};

            var testCases =
                from tentacleType in tentacleTypes
                from halibutType in halibutTypes
                from version in versions.Distinct()
                from stopPortForwarderAfterFirstCall in stopPortForwarderAfterFirstCallValues
                from rpcCall in rpcCalls
                from rpcCallStage in rpcCallStages
                from scriptIsolationLevel in scriptIsolationLevels
                from scriptsInParallelTestCase in scriptsInParallelTestCases
                select new TentacleConfigurationTestCase(
                    tentacleType,
                    halibutType,
                    version,
                    stopPortForwarderAfterFirstCall,
                    rpcCall,
                    rpcCallStage,
                    scriptIsolationLevel,
                    scriptsInParallelTestCase);

            if (additionalParameterTypes.Length == 0)
            {
                return testCases.GetEnumerator();
            }
            
            return CombineTestCasesWithAdditionalParameters(testCases, additionalParameterTypes);
        }

        private static IEnumerator CombineTestCasesWithAdditionalParameters(IEnumerable<TentacleConfigurationTestCase> testCases, object[] additionalEnumerables)
        {
            AllCombinations enums = new AllCombinations();
            var additionalEnums = additionalEnumerables.Cast<Type>().ToArray();

            for (int i = 0; i < additionalEnums.Length; i++)
            {
                var additionalEnum = additionalEnums[i];
                if (additionalEnum.IsEnum)
                {
                    enums.And(Enum.GetValues(additionalEnums[i]));
                }
                else if (typeof(IEnumerable).IsAssignableFrom(additionalEnum))
                {
                    enums.And(ValuesOf.CreateValues(additionalEnum));
                }
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
