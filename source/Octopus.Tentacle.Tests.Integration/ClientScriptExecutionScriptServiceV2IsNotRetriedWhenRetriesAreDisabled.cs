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
using Octopus.Tentacle.Contracts.ClientServices;
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
        [TentacleConfigurations]
        public async Task WhenNetworkFailureOccurs_DuringGetCapabilities_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            IAsyncClientScriptServiceV2? asyncScriptServiceV2 = null;

            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToCapabilitiesServiceV2()
                    .CountCallsToCapabilitiesServiceV2(out var capabilitiesServiceCallCounts)
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .RecordExceptionThrownInCapabilitiesServiceV2(out var capabilitiesServiceExceptions)
                    .DecorateCapabilitiesServiceV2With(new CapabilitiesServiceV2DecoratorBuilder()
                        .BeforeGetCapabilities(async () =>
                        {
                            // Due to the GetCapabilities response getting cached, we must
                            // use a different service to ensure Tentacle is connected to Server.
                            // Otherwise, the response to the 'ensure connection' will get cached
                            // and any subsequent calls will succeed w/o using the network.
                            await asyncScriptServiceV2.EnsureTentacleIsConnectedToServer(Logger);

                            if (capabilitiesServiceExceptions.GetCapabilitiesLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            asyncScriptServiceV2 = clientTentacle.Server.ServerHalibutRuntime.CreateAsyncClient<IScriptServiceV2, IAsyncClientScriptServiceV2>(clientTentacle.ServiceEndPoint);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().Print("hello")).Build();

            var logs = new List<ProcessOutput>();
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken));

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("hello");
            capabilitiesServiceExceptions.GetCapabilitiesLatestException.Should().NotBeNull();
            capabilitiesServiceCallCounts.GetCapabilitiesCallCountStarted.Should().Be(1);
            capabilitiesServiceCallCounts.GetCapabilitiesCallCountComplete.Should().Be(1);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().Be(0);
            scriptServiceCallCounts.GetStatusCallCountCompleted.Should().Be(0);
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(0);
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(0);
        }
        
        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringStartScript_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceExceptions)
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeStartScript(async () =>
                        {
                            await Task.CompletedTask;

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
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken));

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");
            scriptServiceExceptions.StartScriptLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().Be(0);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);

            // We must ensure all script are complete, otherwise if we shutdown tentacle while running a script the build can hang.
            // Ensure the script is finished by running the script again
            await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, new List<ProcessOutput>(), CancellationToken);
        }
        
        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringGetStatus_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceExceptions)
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeGetStatus(async (_, _) =>
                        {
                            await Task.CompletedTask;

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
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken));
            Logger.Information("Exception thrown.");

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");
            scriptServiceExceptions.GetStatusLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);
            
            // Let the script finish.
            File.WriteAllText(waitForFile, "");
            await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, new List<ProcessOutput>(), CancellationToken);
        }
        
        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringCancelScript_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceExceptions)
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeGetStatus(async (_, _) =>
                        {
                            await Task.CompletedTask;

                            cts.Cancel();
                        })
                        .BeforeCancelScript(async (service, _) =>
                        {
                            await Task.CompletedTask;

                            if (scriptServiceExceptions.CancelScriptLatestException == null)
                            {
                                await service.EnsureTentacleIsConnectedToServer(Logger);
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
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, cts.Token));

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");
            scriptServiceExceptions.CancelScriptLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CancelScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);
            
            // Let the script finish.
            File.WriteAllText(waitForFile, "");
            await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, new List<ProcessOutput>(), CancellationToken);
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringCompleteScript_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceExceptions)
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeCompleteScript(async () =>
                        {
                            await Task.CompletedTask;

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
            await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("AllDone");
            scriptServiceExceptions.CompleteScriptLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(1);
        }
    }
}
