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
        [Test]
        [TentacleConfigurations(testScriptIsolationLevel: true)]
        public async Task ScriptIsolationMutexFull_EnsuresTwoDifferentScriptsDontRunAtTheSameTime(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            TentacleType tentacleType = tentacleConfigurationTestCase.TentacleType;
            SyncOrAsyncHalibut syncOrAsyncHalibut = tentacleConfigurationTestCase.SyncOrAsyncHalibut;
            Version? tentacleVersion = tentacleConfigurationTestCase.Version;
            ScriptIsolationLevel levelOfSecondScript = tentacleConfigurationTestCase.ScriptIsolationLevel!.Value;
            
            await using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
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

        [Test]
        [TentacleConfigurations(testScriptsInParallel: true)]
        public async Task ScriptIsolationMutexFull_IsOnlyExclusiveWhenFullAndWhenTheMutexNameIsTheSame(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            TentacleType tentacleType = tentacleConfigurationTestCase.TentacleType;
            SyncOrAsyncHalibut syncOrAsyncHalibut = tentacleConfigurationTestCase.SyncOrAsyncHalibut;
            Version? tentacleVersion = tentacleConfigurationTestCase.Version;
            ScriptsInParallelTestCase scriptsInParallelTestCases = tentacleConfigurationTestCase.ScriptsInParallelTestCase!;
            
            await using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
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
                .WithIsolation(scriptsInParallelTestCases.LevelOfFirstScript)
                .WithMutexName(scriptsInParallelTestCases.MutexForFirstScript)
                .Build();

            var secondStartScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().CreateFile(secondScriptStart))
                .WithIsolation(scriptsInParallelTestCases.LevelOfSecondScript)
                .WithMutexName(scriptsInParallelTestCases.MutexForSecondScript)
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
