using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionIsolationMutex : IntegrationTest
    {
        class WithFullIsolationTestCases : IEnumerable
        {
            public IEnumerator GetEnumerator()
            {
                return AllCombinations
                    .Of(TentacleType.Polling,
                        TentacleType.Listening)
                    .And(
                        TentacleVersions.Current,
                        TentacleVersions.v6_3_417_LastWithScriptServiceV1Only,
                        TentacleVersions.v7_0_1_ScriptServiceV2Added)
                    .And(ScriptIsolationLevel.FullIsolation, ScriptIsolationLevel.NoIsolation)
                    .And(
                        SyncOrAsyncHalibut.Sync,
                        SyncOrAsyncHalibut.Async
                    )
                    .Build();
            }
        }

        [Test]
        [TestCaseSource(typeof(WithFullIsolationTestCases))]
        public async Task ScriptIsolationMutexFull_EnsuresTwoDifferentScriptsDontRunAtTheSameTime(
            TentacleType tentacleType, 
            Version? tentacleVersion, 
            ScriptIsolationLevel levelOfSecondScript,
            SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithAsyncHalibutFeature(syncOrAsyncHalibut.ToAsyncHalibutFeature())
                .WithTentacleVersion(tentacleVersion)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts)
                    .CountCallsToScriptService(out var scriptServiceCallCounts)
                    .Build())
                .Build(CancellationToken);

            var firstScriptStartFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "firstScriptStartFile");
            var firstScriptWaitFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "firstScriptWaitFile");

            var secondScriptStart = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "secondScriptStartFile");

            var firstStartScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .CreateFile(firstScriptStartFile)
                    .WaitForFileToExist(firstScriptWaitFile))
                .WithIsolation(ScriptIsolationLevel.FullIsolation)
                .WithMutexName("mymutex")
                .Build();

            var secondStartScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().CreateFile(secondScriptStart))
                .WithIsolation(levelOfSecondScript)
                .WithMutexName("mymutex")
                .Build();

            var tentacleClient = clientTentacle.TentacleClient;
            var firstScriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(firstStartScriptCommand, CancellationToken));

            // Wait for the first script to start running
            await Wait.For(() => File.Exists(firstScriptStartFile), CancellationToken);
            Logger.Information("First script is now running");

            var secondScriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(secondStartScriptCommand, CancellationToken));

            // Wait for the second script start script RPC call to return.
            await Wait.For(() => (scriptServiceV2CallCounts.StartScriptCallCountComplete + scriptServiceCallCounts.StartScriptCallCountComplete) == 2, CancellationToken);

            // Give Tentacle some more time to run the script (although it should not).
            await Task.Delay(TimeSpan.FromSeconds(2));

            File.Exists(secondScriptStart).Should().BeFalse("The second script must not be started while the first is running with a FullIsolationMutex");

            // Let the first script finish.
            File.WriteAllText(firstScriptWaitFile, "");

            var (finalResponseFirstScript, _) = await firstScriptExecution;
            var (finalResponseSecondScript, _) = await secondScriptExecution;

            File.Exists(secondScriptStart).Should().BeTrue("The second should now have run.");

            finalResponseFirstScript.ExitCode.Should().Be(0);
            finalResponseSecondScript.ExitCode.Should().Be(0);
        }

        public class ScriptsInParallelTestCases
        {
            public ScriptIsolationLevel levelOfFirstScript;
            public string mutexForFirstScript;
            public ScriptIsolationLevel levelOfSecondScript;
            public string mutexForSecondScript;

            public ScriptsInParallelTestCases(ScriptIsolationLevel levelOfFirstScript, string mutexForFirstScript, ScriptIsolationLevel levelOfSecondScript, string mutexForSecondScript)
            {
                this.levelOfFirstScript = levelOfFirstScript;
                this.mutexForFirstScript = mutexForFirstScript;
                this.levelOfSecondScript = levelOfSecondScript;
                this.mutexForSecondScript = mutexForSecondScript;
            }
        }

        class ScriptsCanRunInParallelCases : IEnumerable
        {
            public IEnumerator GetEnumerator()
            {
                return AllCombinations
                    .Of(TentacleType.Polling,
                        TentacleType.Listening)
                    .And(
                        TentacleVersions.Current,
                        TentacleVersions.v6_3_417_LastWithScriptServiceV1Only,
                        TentacleVersions.v7_0_1_ScriptServiceV2Added) // Testing against v1 and v2 script services
                    .And(
                        // Scripts with different mutex names can run at the same time.
                        new ScriptsInParallelTestCases(ScriptIsolationLevel.FullIsolation, "mutex", ScriptIsolationLevel.FullIsolation, "differentMutex"),
                        // Scripts with the same mutex name can run at the same time if they both has no isolation.
                        new ScriptsInParallelTestCases(ScriptIsolationLevel.NoIsolation, "sameMutex", ScriptIsolationLevel.NoIsolation, "sameMutex"))
                    .And(
                        SyncOrAsyncHalibut.Sync,
                        SyncOrAsyncHalibut.Async
                    )
                    .Build();
            }
        }

        [Test]
        [TestCaseSource(typeof(ScriptsCanRunInParallelCases))]
        public async Task ScriptIsolationMutexFull_IsOnlyExclusiveWhenFullAndWhenTheMutexNameIsTheSame(
            TentacleType tentacleType,
            Version tentacleVersion,
            ScriptsInParallelTestCases scriptsInParallelTestCases,
            SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithAsyncHalibutFeature(syncOrAsyncHalibut.ToAsyncHalibutFeature())
                .WithTentacleVersion(tentacleVersion)
                .Build(CancellationToken);

            var firstScriptStartFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "firstScriptStartFile");
            var firstScriptWaitFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "firstScriptWaitFile");

            var secondScriptStart = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "secondScriptStartFile");

            var firstStartScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .CreateFile(firstScriptStartFile)
                    .WaitForFileToExist(firstScriptWaitFile))
                .WithIsolation(scriptsInParallelTestCases.levelOfFirstScript)
                .WithMutexName(scriptsInParallelTestCases.mutexForFirstScript)
                .Build();

            var secondStartScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().CreateFile(secondScriptStart))
                .WithIsolation(scriptsInParallelTestCases.levelOfSecondScript)
                .WithMutexName(scriptsInParallelTestCases.mutexForSecondScript)
                .Build();

            var tentacleClient = clientTentacle.TentacleClient;
            var firstScriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(firstStartScriptCommand, CancellationToken));

            // Wait for the first script to start running
            await Wait.For(() => File.Exists(firstScriptStartFile), CancellationToken);

            var secondScriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(secondStartScriptCommand, CancellationToken));

            // Wait for the second script to start
            await Wait.For(() => File.Exists(secondScriptStart), CancellationToken);
            // Both scripts are now running in parallel

            // Let the first script finish.
            File.WriteAllText(firstScriptWaitFile, "");

            var (finalResponseFirstScript, _) = await firstScriptExecution;
            var (finalResponseSecondScript, _) = await secondScriptExecution;

            File.Exists(secondScriptStart).Should().BeTrue("The second script must not be started while the first is running with a FullIsolationMutex");

            finalResponseFirstScript.ExitCode.Should().Be(0);
            finalResponseSecondScript.ExitCode.Should().Be(0);
        }
    }
}