using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionScriptServiceV2IsNotRetriedWhenRetriesAreDisabled : IntegrationTest
    {
        [Test]
        [TestCaseSource(typeof(TentacleTypesToTest))]
        public async Task WhenANetworkFailureOccurs_DuringStartScript_TheCallIsNotRetried(TentacleType tentacleType)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceExceptions)
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeStartScript(() =>
                        {
                            if (scriptServiceExceptions.StartScriptLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .Print("AllDone"))
                .Build();

            var logs = new List<ProcessOutput>();
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScriptAssumingException(startScriptCommand, logs, CancellationToken));

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");
            scriptServiceExceptions.StartScriptLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().Be(0);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);

            // We must ensure all script are complete, otherwise if we shutdown tentacle while running a script the build can hang.
            // Ensure the script is finished by running the script again
            await clientTentacle.TentacleClient.ExecuteScriptAssumingException(startScriptCommand, new List<ProcessOutput>(), CancellationToken);
        }
        
        [Test]
        [TestCaseSource(typeof(TentacleTypesToTest))]
        public async Task WhenANetworkFailureOccurs_DuringGetStatus_TheCallIsNotRetried(TentacleType tentacleType)
        {
            ScriptStatusRequestV2? scriptStatusRequest = null;
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceExceptions)
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeGetStatus((inner, request) =>
                        {
                            scriptStatusRequest = request;
                            if (scriptServiceExceptions.GetStatusLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();

            List<ProcessOutput> logs = new List<ProcessOutput>();
            Logger.Information("Starting and waiting for script exec");
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScriptAssumingException(startScriptCommand, logs, CancellationToken));
            Logger.Information("Exception thrown.");

            // Let the script finish.
            File.WriteAllText(waitForFile, "");
            var legacyTentacleClient = clientTentacle.LegacyTentacleClientBuilder().Build(CancellationToken);
            await Wait.For(() => legacyTentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(scriptStatusRequest.Ticket, scriptStatusRequest.LastLogSequence)).State == ProcessState.Complete, CancellationToken);

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");
            scriptServiceExceptions.GetStatusLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);
        }
        
        [Test]
        [TestCaseSource(typeof(TentacleTypesToTest))]
        public async Task WhenANetworkFailureOccurs_DuringCancelScript_TheCallIsNotRetried(TentacleType tentacleType)
        {
            ScriptStatusRequestV2? scriptStatusRequest = null;
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceExceptions)
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeGetStatus((inner, request) =>
                        {
                            cts.Cancel();
                            scriptStatusRequest = request;
                        })
                        .BeforeCancelScript(() =>
                        {
                            if (scriptServiceExceptions.CancelScriptLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();

            List<ProcessOutput> logs = new List<ProcessOutput>();

            //Assert.CatchAsync(async () => await clientTentacle.TentacleClient.ExecuteScriptAssumingException(startScriptCommand, logs, cts.Token));
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScriptAssumingException(startScriptCommand, logs, cts.Token));

            // Let the script finish.
            File.WriteAllText(waitForFile, "");
            var legacyTentacleClient = clientTentacle.LegacyTentacleClientBuilder().Build(CancellationToken);
            await Wait.For(() => legacyTentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(scriptStatusRequest.Ticket, scriptStatusRequest.LastLogSequence)).State == ProcessState.Complete, CancellationToken);

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");
            scriptServiceExceptions.CancelScriptLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CancelScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesToTest))]
        public async Task WhenANetworkFailureOccurs_DuringCompleteScript_TheCallIsNotRetried(TentacleType tentacleType)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceExceptions)
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeCompleteScript(() =>
                        {
                            if (scriptServiceExceptions.CompleteScriptLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);
            
            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .Print("AllDone"))
                .Build();

            List<ProcessOutput> logs = new List<ProcessOutput>();
            await clientTentacle.TentacleClient.ExecuteScriptAssumingException(startScriptCommand, logs, CancellationToken);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("AllDone");
            scriptServiceExceptions.CompleteScriptLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(1);
        }
    }
}
