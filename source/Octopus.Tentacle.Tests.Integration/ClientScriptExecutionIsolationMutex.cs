using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
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
        [TentacleConfigurations(testScriptIsolationLevelVersions: true, additionalParameterTypes: new object[] { typeof(ScriptIsolationLevel)})]
        public async Task ScriptIsolationMutexFull_EnsuresTwoDifferentScriptsDontRunAtTheSameTime(TentacleConfigurationTestCase tentacleConfigurationTestCase, ScriptIsolationLevel levelOfSecondScript)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var tracingStats)
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
            await Wait.For(() => tracingStats.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Completed == 2, CancellationToken);

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
        [TentacleConfigurations(testScriptIsolationLevelVersions: true, additionalParameterTypes: new object[] {typeof(ScriptsInParallelTestCases)})]
        public async Task ScriptIsolationMutexFull_IsOnlyExclusiveWhenFullAndWhenTheMutexNameIsTheSame(TentacleConfigurationTestCase tentacleConfigurationTestCase, ScriptsInParallelTestCase scriptsInParallelTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder().Build(CancellationToken);

            var firstScriptStartFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "firstScriptStartFile");
            var firstScriptWaitFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "firstScriptWaitFile");

            var secondScriptStart = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "secondScriptStartFile");

            var firstStartScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .CreateFile(firstScriptStartFile)
                    .WaitForFileToExist(firstScriptWaitFile))
                .WithIsolation(scriptsInParallelTestCase.LevelOfFirstScript)
                .WithMutexName(scriptsInParallelTestCase.MutexForFirstScript)
                .Build();

            var secondStartScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().CreateFile(secondScriptStart))
                .WithIsolation(scriptsInParallelTestCase.LevelOfSecondScript)
                .WithMutexName(scriptsInParallelTestCase.MutexForSecondScript)
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

        public class ScriptsInParallelTestCases : IEnumerable
        {
            public IEnumerator GetEnumerator()
            {
                // Scripts with the same mutex name can run at the same time if they both has no isolation.
                yield return ScriptsInParallelTestCase.NoIsolationSameMutex;
                // Scripts with different mutex names can run at the same time.
                yield return ScriptsInParallelTestCase.FullIsolationDifferentMutex;
            }
        }

        public class ScriptsInParallelTestCase
        {
            public static ScriptsInParallelTestCase NoIsolationSameMutex => new(ScriptIsolationLevel.NoIsolation, "sameMutex", ScriptIsolationLevel.NoIsolation, "sameMutex", nameof(NoIsolationSameMutex));
            public static ScriptsInParallelTestCase FullIsolationDifferentMutex => new(ScriptIsolationLevel.FullIsolation, "mutex", ScriptIsolationLevel.FullIsolation, "differentMutex", nameof(FullIsolationDifferentMutex));

            public readonly ScriptIsolationLevel LevelOfFirstScript;
            public readonly string MutexForFirstScript;
            public readonly ScriptIsolationLevel LevelOfSecondScript;
            public readonly string MutexForSecondScript;

            private readonly string stringValue;

            private ScriptsInParallelTestCase(
                ScriptIsolationLevel levelOfFirstScript,
                string mutexForFirstScript,
                ScriptIsolationLevel levelOfSecondScript,
                string mutexForSecondScript,
                string stringValue)
            {
                LevelOfFirstScript = levelOfFirstScript;
                MutexForFirstScript = mutexForFirstScript;
                LevelOfSecondScript = levelOfSecondScript;
                MutexForSecondScript = mutexForSecondScript;
                this.stringValue = stringValue;
            }

            public override string ToString()
            {
                return stringValue;
            }
        }
    }
}
