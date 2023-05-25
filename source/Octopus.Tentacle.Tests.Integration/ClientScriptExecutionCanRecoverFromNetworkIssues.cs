using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ClientScriptExecutionCanRecoverFromNetworkIssues : IntegrationTest
    {
        [Test]
        public async Task WhenANetworkFailureOccurs_DuringStartScript_TheClientIsAbleToSuccessfullyCompleteTheScript()
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(TentacleType.Polling)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder().CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts).Build())
                .Build(CancellationToken);
            using var tmp = new TemporaryDirectory();

            var scriptHasStartFile = Path.Combine(tmp.DirectoryPath, "scripthasstarted");
            var waitForFile = Path.Combine(tmp.DirectoryPath, "waitforme");

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .CreateFile(scriptHasStartFile)
                    .WaitForFileToExist(waitForFile))
                // Configure the start script command to wait a long time, so we have plenty of time to kill the connection.
                .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromHours(1))
                .Build();

            var execScriptTask = Task.Run(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken), CancellationToken);

            // Wait for the script to start.
            await Wait.For(() => File.Exists(scriptHasStartFile), CancellationToken);

            // Now it has started, kill active connections killing the start script request.
            clientTentacle.PortForwarder.CloseExistingConnections();

            // Let the script finish.
            File.WriteAllText(waitForFile, "");

            var (finalResponse, logs) = await execScriptTask;

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");

            scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().BeGreaterThan(1);
        }

        [Test]
        public async Task WhenANetworkFailureOccurs_DuringGetStatus_TheClientIsAbleToSuccessfullyCompleteTheScript()
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(TentacleType.Polling)
                .WithPollingResponseMessageTcpKiller(out var pollingResponseMessageTcpKiller)
                .WithPortForwarder(builder => builder.WithDataLoggingForPolling())
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceV2Exceptions)
                    .LogCallsToScriptServiceV2()
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeGetStatus(() =>
                        {
                            if (scriptServiceV2Exceptions.GetStatusLatestException == null)
                            {
                                pollingResponseMessageTcpKiller.KillConnectionOnNextResponse();
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

            var execScriptTask = Task.Run(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken), CancellationToken);

            await Wait.For(() => scriptServiceV2Exceptions.GetStatusLatestException != null, CancellationToken);

            // Let the script finish.
            File.WriteAllText(waitForFile, "");

            var (finalResponse, logs) = await execScriptTask;

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");
            scriptServiceV2Exceptions.GetStatusLatestException.Should().NotBeNull();
        }

        [Test]
        public async Task WhenANetworkFailureOccurs_DuringCompleteScript_TheClientIsAbleToSuccessfullyCompleteTheScript()
        {
            bool completeScriptWasCalled = false;
            var portForwarderRef = new Reference<PortForwarder>();
            using var clientTentacle = await new ClientAndTentacleBuilder(TentacleType.Polling)
                .WithPortForwarder(builder => builder.WithDataLoggingForPolling())
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithServiceEndpointModifier(serviceEndpoint =>
                {
                    serviceEndpoint.PollingRequestQueueTimeout = TimeSpan.FromSeconds(10);
                    serviceEndpoint.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(10);
                })
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeCompleteScript(() =>
                        {
                            completeScriptWasCalled = true;
                            // A successfully CompleteScript call is not required for the script to be completed.
                            // So it should be the case that the tentacle can be no longer contactable at this point,
                            // yet the script execution is marked as successful.
                            portForwarderRef.value.Dispose();
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);
            portForwarderRef.value = clientTentacle.PortForwarder;

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().Print("hello").Sleep(TimeSpan.FromSeconds(1)))
                .Build();

            var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");
            completeScriptWasCalled.Should().BeTrue("The tests expects that the client actually called this");
        }

        [Test]
        public async Task WhenANetworkFailureOccurs_DuringCancelScript_TheClientIsAbleToSuccessfullyCancelTheScript()
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            using var clientTentacle = await new ClientAndTentacleBuilder(TentacleType.Polling)
                .WithPollingResponseMessageTcpKiller(out var pollingResponseMessageTcpKiller)
                .WithPortForwarder(builder => builder.WithDataLoggingForPolling())
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceV2Exceptions)
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeGetStatus(() => cts.Cancel())
                        .BeforeCancelScript(() =>
                        {
                            if (scriptServiceV2Exceptions.CancelScriptLatestException == null)
                            {
                                pollingResponseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .Sleep(TimeSpan.FromMinutes(2))
                    .Print("AllDone"))
                .Build();

            var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, cts.Token);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().NotBe(0);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");
            allLogs.Should().NotContain("AllDone");
            scriptServiceV2Exceptions.CancelScriptLatestException.Should().NotBeNull();
        }

        [Test]
        public async Task WhenANetworkFailureOccurs_DuringGetCapabilities_TheClientIsAbleToSuccessfullyCompleteTheScript()
        {
            var token = TestCancellationToken.Token();
            var logger = new SerilogLoggerBuilder().Build();
            var queue = new IsTentacleWaitingPendingRequestQueueDecoratorFactory();

            Exception exceptionInCallToGetCapabilities = null;
            using var clientTentacle = await new ClientAndTentacleBuilder(TentacleType.Polling)
                .WithPollingResponseMessageTcpKiller(out var pollingResponseMessageTcpKiller)
                .WithPortForwarder(builder => builder.WithDataLoggingForPolling())
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithPendingRequestQueueFactory(queue)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateCapabilitiesServiceV2With(new CapabilitiesServiceV2DecoratorBuilder()
                        .DecorateGetCapabilitiesWith(inner =>
                        {
                            logger.Information("Calling GetCapabilities");
                            if (exceptionInCallToGetCapabilities == null)
                            {
                                pollingResponseMessageTcpKiller.KillConnectionOnNextResponse();
                            }

                            try
                            {
                                return inner.GetCapabilities();
                            }
                            catch (Exception e)
                            {
                                exceptionInCallToGetCapabilities = e;
                                logger.Information("Error in GetCapabilities" + e);
                                throw;
                            }
                            finally
                            {
                                logger.Information("GetCapabilities call complete");
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            await queue.WaitUntilATentacleIsWaitingToDequeueAMessage(token);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().Print("hello"))
                .Build();

            var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, token);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");
            exceptionInCallToGetCapabilities.Should().NotBeNull();
        }
    }
}