using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client;
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
    public class ClientScriptExecutionScriptServiceV1IsNotRetried : IntegrationTest
    {
        [Test]
        [TestCaseSource(typeof(TentacleTypesToTest))]
        public async Task WhenANetworkFailureOccurs_DuringStartScript_WithATentacleThatOnlySupportsV1ScriptService_TheCallIsNotRetried(TentacleType tentacleType)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion("6.3.451") // No capabilities service
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptService()
                    .RecordExceptionThrownInScriptService(out var scriptServiceExceptions)
                    .CountCallsToScriptService(out var scriptServiceCallCounts)
                    .DecorateScriptServiceWith(new ScriptServiceDecoratorBuilder()
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

            using var tmp = new TemporaryDirectory();
            var waitForFile = Path.Combine(tmp.DirectoryPath, "waitforme");

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();


            List<ProcessOutput> logs = new List<ProcessOutput>();
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScriptAssumingException(startScriptCommand, logs, CancellationToken));

            // Let the script finish.
            File.WriteAllText(waitForFile, "");

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");
            scriptServiceExceptions.StartScriptLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().Be(0);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);
        }
        
        [Test]
        [TestCaseSource(typeof(TentacleTypesToTest))]
        public async Task WhenANetworkFailureOccurs_DuringGetStatus_WithATentacleThatOnlySupportsV1ScriptService_TheCallIsNotRetried(TentacleType tentacleType)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion("6.3.451") // No capabilities service
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptService()
                    .RecordExceptionThrownInScriptService(out var scriptServiceExceptions)
                    .CountCallsToScriptService(out var scriptServiceCallCounts)
                    .DecorateScriptServiceWith(new ScriptServiceDecoratorBuilder()
                        .BeforeGetStatus(() =>
                        {
                            if (scriptServiceExceptions.GetStatusLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            using var tmp = new TemporaryDirectory();
            var waitForFile = Path.Combine(tmp.DirectoryPath, "waitforme");

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();


            List<ProcessOutput> logs = new List<ProcessOutput>();
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScriptAssumingException(startScriptCommand, logs, CancellationToken));

            // Let the script finish.
            File.WriteAllText(waitForFile, "");

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");
            scriptServiceExceptions.GetStatusLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);
        }
        
        [Test]
        [TestCaseSource(typeof(TentacleTypesToTest))]
        public async Task WhenANetworkFailureOccurs_DuringCompleteScript_WithATentacleThatOnlySupportsV1ScriptService_TheCallIsNotRetried(TentacleType tentacleType)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion("6.3.451") // No capabilities service
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptService()
                    .RecordExceptionThrownInScriptService(out var scriptServiceExceptions)
                    .CountCallsToScriptService(out var scriptServiceCallCounts)
                    .DecorateScriptServiceWith(new ScriptServiceDecoratorBuilder()
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

            var startScriptCommand = new StartScriptCommandV2Builder().WithScriptBody(new ScriptBuilder().Print("hello")).Build();


            List<ProcessOutput> logs = new List<ProcessOutput>();
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScriptAssumingException(startScriptCommand, logs, CancellationToken));

            // We Can not verify what will be in the logs because of race conditions in tentacle.
            // The last complete script which we fail might come back with the logs.
            
            scriptServiceExceptions.CompleteScriptLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(1);
        }
    }
    
    
    static class TentacleClientExtensionMethods
    {
        public static async Task<ScriptStatusResponseV2> ExecuteScriptAssumingException(
            this TentacleClient tentacleClient, 
            StartScriptCommandV2 startScriptCommand,
            List<ProcessOutput> logs,
            CancellationToken token)
        {
            var finalResponse = await tentacleClient.ExecuteScript(startScriptCommand,
                onScriptStatusResponseReceived => logs.AddRange(onScriptStatusResponseReceived.Logs),
                cts => Task.CompletedTask,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToILog(),
                token);
            return (finalResponse);
        }
    }
}