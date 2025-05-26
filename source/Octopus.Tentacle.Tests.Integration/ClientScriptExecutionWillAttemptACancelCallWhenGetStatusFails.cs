using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionWillAttemptACancelCallWhenGetStatusFails : IntegrationTest
    {
        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringGetStatus_AndTheGetStatusCallDoesNotRecover_AnAttemptToCancelTheScriptIsMade(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithRetryDuration(TimeSpan.FromSeconds(10))
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimits
                {
                    RetryCountLimit = 1,
                    ConnectionErrorRetryTimeout = TimeSpan.FromSeconds(1),
                    RetryListeningSleepInterval = TimeSpan.FromSeconds(1),
                    PollingRequestQueueTimeout = TimeSpan.FromSeconds(5)
                })
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .DecorateAllScriptServicesWith(u => u
                        .BeforeGetStatus(
                            async () =>
                            {
                                await Task.CompletedTask;
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            })
                    )
                    .Build())
                .Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");
            var fileCreatedByScript = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "scriptFile");

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone")
                    .CreateFile(fileCreatedByScript))
                .Build();

            var inMemoryLog = new InMemoryLog();

            var execScriptTask = Task.Run(
                async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog),
                CancellationToken);

            await Wait.For(() => recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).LastException != null,
                TimeSpan.FromSeconds(60),
                () => throw new Exception("GetStatus did not error"),
                CancellationToken);

            // Assert

            // We should find that the cancel request was sent to tentacle.
            await Wait.For(() => recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Completed > 0,
                TimeSpan.FromSeconds(60),
                () => throw new Exception("Cancel script was never called."),
                CancellationToken);

            // Let the script finish.
            File.WriteAllText(waitForFile, "");
            await Task.Delay(TimeSpan.FromSeconds(2)); // It's hard to know if the script was killed or not, if we do the assert below too quickly we will
            // incorrectly see the script as being cancelled.

            File.Exists(fileCreatedByScript).Should().BeFalse("A cancel command should have been sent to the script, resulting in the script terminating");
        }
    }
}