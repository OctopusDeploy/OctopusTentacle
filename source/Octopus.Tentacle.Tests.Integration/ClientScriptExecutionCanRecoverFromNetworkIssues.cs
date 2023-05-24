using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ClientScriptExecutionCanRecoverFromNetworkIssues
    {
        [Test]
        public async Task WhenANetworkFailureOccurs_DuringStartScript_TheClientIsAbleToSuccessfullyCompleteTheScript()
        {
            var token = TestCancellationToken.Token();
            var logger = new SerilogLoggerBuilder().Build();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var tmp = new TemporaryDirectory())
            using (var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(port).Build())
            using (var runningTentacle = await new PollingTentacleBuilder(portForwarder.ListeningPort, Support.Certificates.ServerPublicThumbprint).Build(token))
            {
                var serviceEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

                var scriptHasStartFile = Path.Combine(tmp.DirectoryPath, "scripthasstarted");
                var waitForFile = Path.Combine(tmp.DirectoryPath, "waitforme");

                int startScriptCallCount = 0;
                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceV2With(
                        builder => builder.DecorateStartScriptWith((inner, command) =>
                            {
                                startScriptCallCount++;
                                return inner.StartScript(command);
                            }))
                    .Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), tentacleServicesDecorator, TimeSpan.FromMinutes(4));

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder()
                        .Print("hello")
                        .CreateFile(scriptHasStartFile)
                        .WaitForFileToExist(waitForFile))
                    // Configure the start script command to wait a long time, so we have plenty of time to kill the connection.
                    .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromHours(1))
                    .Build();
                
                var execScriptTask = Task.Run(async () => await tentacleClient.ExecuteScript(startScriptCommand, token), token);

                // Wait for the script to start.
                await Wait.For(() => File.Exists(scriptHasStartFile), token);

                // Now it has started, kill active connections killing the start script request.
                portForwarder.CloseExistingConnections();

                // Let the script finish.
                File.WriteAllText(waitForFile, "");

                var (finalResponse, logs) = await execScriptTask;

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);

                var allLogs = JoinLogs(logs);
                allLogs.Should().Contain("hello");

                startScriptCallCount.Should().BeGreaterThan(1);
            }
        }

        [Test]
        public async Task WhenANetworkFailureOccurs_DuringGetStatus_TheClientIsAbleToSuccessfullyCompleteTheScript()
        {
            var token = TestCancellationToken.Token();
            var logger = new SerilogLoggerBuilder().Build();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            Reference<PortForwarder> portForwarderRef= new Reference<PortForwarder>();
            Reference<Boolean> killConnectionWhenReceivingResponse = new Reference<Boolean>();
            var dataTransferredFromTentacle = ConnectionKillerWhenReceivingDataFromTentacle(logger, killConnectionWhenReceivingResponse, portForwarderRef);
            
            Exception exceptionInCallToGetStatus = null;

            using (var tmp = new TemporaryDirectory())
            using (var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(port)
                       .WithDataObserver(() => new BiDirectionalDataTransferObserverBuilder().ObserveDataFromRemote(dataTransferredFromTentacle).Build())
                       .Build())
            using (var runningTentacle = await new PollingTentacleBuilder(portForwarder.ListeningPort, Support.Certificates.ServerPublicThumbprint).Build(token))
            {
                portForwarderRef.value = portForwarder;
                var serviceEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

                var waitForFile = Path.Combine(tmp.DirectoryPath, "waitforme");

                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .DecorateGetStatusWith((inner, command) =>
                        {
                            logger.Information("Calling get status");
                            if (exceptionInCallToGetStatus == null)
                            {
                                // Ensure plenty of time for control messages to be sent.
                                Thread.Sleep(1000);
                                killConnectionWhenReceivingResponse.value = true;
                            }

                            try
                            {
                                return inner.GetStatus(command);
                            }
                            catch (Exception e)
                            {
                                exceptionInCallToGetStatus = e;
                                logger.Information("Error in get status " + e);
                                throw;
                            }
                            finally
                            {
                                logger.Information("Get status call complete");
                            }
                        })
                        .Build())
                    .Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), tentacleServicesDecorator, TimeSpan.FromMinutes(4));

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder()
                        .Print("hello")
                        .WaitForFileToExist(waitForFile)
                        .Print("AllDone"))
                    .Build();

                var execScriptTask = Task.Run(async () => await tentacleClient.ExecuteScript(startScriptCommand, token), token);
                
                await Wait.For(() => exceptionInCallToGetStatus != null, token);

                // Let the script finish.
                File.WriteAllText(waitForFile, "");

                var (finalResponse, logs) = await execScriptTask;

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);

                var allLogs = JoinLogs(logs);
                allLogs.Should().Contain("hello");
                exceptionInCallToGetStatus.Should().NotBeNull();
            }
        }

        [Test]
        public async Task WhenANetworkFailureOccurs_DuringCompleteScript_TheClientIsAbleToSuccessfullyCompleteTheScript()
        {
            var token = TestCancellationToken.Token();
            var logger = new SerilogLoggerBuilder().Build();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);
            
            using (var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(port).Build())
            using (var runningTentacle = await new PollingTentacleBuilder(portForwarder.ListeningPort, Support.Certificates.ServerPublicThumbprint).Build(token))
            {
                var serviceEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);
                serviceEndPoint.PollingRequestQueueTimeout = TimeSpan.FromSeconds(10);
                serviceEndPoint.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(10);

                Boolean completeScriptWasCalled = false;
                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .DecorateCompleteScriptWith((inner, command) =>
                        {
                            completeScriptWasCalled = true;
                            // A successfully CompleteScript call is not required for the script to be completed.
                            // So it should be the case that the tentacle can be no longer contactable at this point,
                            // yet the script execution is marked as successful.
                            portForwarder.Dispose();
                            inner.CompleteScript(command);
                        })
                        .Build())
                    .Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), tentacleServicesDecorator, TimeSpan.FromMinutes(4));

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder().Print("hello").Sleep(TimeSpan.FromSeconds(1)))
                    .Build();

                var(finalResponse, logs) = await tentacleClient.ExecuteScript(startScriptCommand, token);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);

                var allLogs = JoinLogs(logs);
                allLogs.Should().Contain("hello");
                completeScriptWasCalled.Should().BeTrue("The tests expects that the client actually called this");
            }
        }

        [Test]
        public async Task WhenANetworkFailureOccurs_DuringCancelScript_TheClientIsAbleToSuccessfullyCancelTheScript()
        {
            var token = TestCancellationToken.Token();
            var logger = new SerilogLoggerBuilder().Build();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);
            
            Exception exceptionInCallToCancelScript = null;

            Reference<PortForwarder> portForwarderRef= new Reference<PortForwarder>();
            Reference<Boolean> killConnectionWhenReceivingResponse = new Reference<Boolean>();
            var dataTransferredFromTentacle = ConnectionKillerWhenReceivingDataFromTentacle(logger, killConnectionWhenReceivingResponse, portForwarderRef);
            
            using (var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(port)
                       .WithDataObserver(() => new BiDirectionalDataTransferObserverBuilder().ObserveDataFromRemote(dataTransferredFromTentacle).Build())
                       .Build())
            using (var runningTentacle = await new PollingTentacleBuilder(portForwarder.ListeningPort, Support.Certificates.ServerPublicThumbprint).Build(token))
            {
                portForwarderRef.value = portForwarder;
                var serviceEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

                CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token);

                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .DecorateGetStatusWith((inner, command) =>
                        {
                            cts.Cancel();
                            return inner.GetStatus(command);
                        })
                        .DecorateCancelScriptWith((inner, command) =>
                        {
                            logger.Information("Calling CancelScript");
                            if (exceptionInCallToCancelScript == null)
                            {
                                // Ensure plenty of time for control messages to be sent.
                                Thread.Sleep(1000);
                                killConnectionWhenReceivingResponse.value = true;
                            }

                            try
                            {
                                return inner.CancelScript(command);
                            }
                            catch (Exception e)
                            {
                                exceptionInCallToCancelScript = e;
                                logger.Information("Error in CancelScript" + e);
                                throw;
                            }
                            finally
                            {
                                logger.Information("CancelScript call complete");
                            }
                        })
                        .Build())
                    .Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), tentacleServicesDecorator, TimeSpan.FromMinutes(4));

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder()
                        .Print("hello")
                        .Sleep(TimeSpan.FromMinutes(2))
                        .Print("AllDone"))
                    .Build();

                var(finalResponse, logs) = await tentacleClient.ExecuteScript(startScriptCommand, cts.Token);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().NotBe(0);

                var allLogs = JoinLogs(logs);
                allLogs.Should().Contain("hello");
                allLogs.Should().NotContain("AllDone");
                exceptionInCallToCancelScript.Should().NotBeNull();
            }
        }

        [Test]
        public async Task WhenANetworkFailureOccurs_DuringGetCapabilities_TheClientIsAbleToSuccessfullyCompleteTheScript()
        {
            var token = TestCancellationToken.Token();
            var logger = new SerilogLoggerBuilder().Build();
            var queue = new IsTentacleWaitingPendingRequestQueueDecoratorFactory();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithPendingRequestQueueFactory(queue)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            Reference<PortForwarder> portForwarderRef= new Reference<PortForwarder>();
            Reference<Boolean> killConnectionWhenReceivingResponse = new Reference<Boolean>();
            var dataTransferredFromTentacle = ConnectionKillerWhenReceivingDataFromTentacle(logger, killConnectionWhenReceivingResponse, portForwarderRef);
            
            Exception exceptionInCallToGetCapabilities = null; 
            
            using (var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(port)
                       .WithDataObserver(() => new BiDirectionalDataTransferObserverBuilder().ObserveDataFromRemote(dataTransferredFromTentacle).Build())
                       .Build())
            using (var runningTentacle = await new PollingTentacleBuilder(portForwarder.ListeningPort, Support.Certificates.ServerPublicThumbprint).Build(token))
            {
                portForwarderRef.value = portForwarder;
                var serviceEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

                await queue.WaitUntilATentacleIsWaitingToDequeueAMessage(token);

                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder()
                    .DecorateCapabilitiesServiceV2With(new CapabilitiesServiceV2DecoratorBuilder()
                        .DecorateGetCapabilitiesWith(inner =>
                        {
                            logger.Information("Calling GetCapabilities");
                            if (exceptionInCallToGetCapabilities == null)
                            {
                                // Ensure plenty of time for control messages to be sent.
                                Thread.Sleep(1000);
                                killConnectionWhenReceivingResponse.value = true;
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
                    .Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), tentacleServicesDecorator, TimeSpan.FromMinutes(4));

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder().Print("hello"))
                    .Build();

                var(finalResponse, logs) = await tentacleClient.ExecuteScript(startScriptCommand, token);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);

                var allLogs = JoinLogs(logs);
                allLogs.Should().Contain("hello");
                exceptionInCallToGetCapabilities.Should().NotBeNull();
            }
        }

        /// <summary>
        /// Will only kill the connection when data is being received from the Tentacle but before being sent to the client.
        ///
        /// Useful for when the aim is to kill a in-flight request. 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="killConnection"></param>
        /// <param name="portForwarderRef"></param>
        /// <returns></returns>
        private static IDataTransferObserver ConnectionKillerWhenReceivingDataFromTentacle(ILogger logger, Reference<bool> killConnection, Reference<PortForwarder> portForwarderRef)
        {
            return new DataTransferObserverBuilder().WithWritingDataObserver(dataFromTentacle =>
            {
                var size = dataFromTentacle.Length;
                logger.Information($"Received: {size} from tentacle");
                if (killConnection.value)
                {
                    killConnection.value = false;
                    logger.Information("Killing connection");
                    portForwarderRef.value.CloseExistingConnections();
                }
            }).Build();
        }

        private static string JoinLogs(List<ProcessOutput> logs)
        {
            return String.Join(" ", logs.Select(l => l.Text).ToArray());
        }
    }
}