using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;
using Octopus.TestPortForwarder;

namespace Octopus.Tentacle.Tests.Integration
{
    /// <summary>
    /// These tests make sure that we can cancel or walk away (if code does not cooperate with cancellation tokens)
    /// from RPC calls when they are being retried and the rpc timeout period elapses.
    /// </summary>
    [IntegrationTestTimeout]
    public class ClientFileTransferRetriesTimeout : IntegrationTest
    {
        [Test]
        [TestCase(TentacleType.Polling, false)] // Timeout an in-flight request
        [TestCase(TentacleType.Polling, true)] // Timeout trying to connect
        [TestCase(TentacleType.Listening, false)] // Timeout an in-flight request
        [TestCase(TentacleType.Listening, true)] // Timeout trying to connect
        public async Task WhenRpcRetriesTimeOut_DuringUploadFile_TheRpcCallIsCancelled(TentacleType tentacleType, bool stopPortForwarderAfterFirstCall)
        {
            PortForwarder portForwarder = null!;
            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToFileTransferService()
                    .CountCallsToFileTransferService(out var fileTransferServiceCallCounts)
                    .RecordExceptionThrownInFileTransferService(out var fileTransferServiceException)
                    .DecorateFileTransferServiceWith(d => d
                        .BeforeUploadFile(async (service, _, _) =>
                        {
                            await service.EnsureTentacleIsConnectedToServer(Logger);

                            // Kill the first UploadFile call to force the rpc call into retries
                            if (fileTransferServiceException.UploadLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                if (stopPortForwarderAfterFirstCall)
                                {
                                    // Kill the port forwarder so the next requests are in the connecting state when retries timeout
                                    Logger.Information("Killing PortForwarder");
                                    portForwarder!.Dispose();
                                }
                                else
                                {
                                    // Pause the port forwarder so the next requests are in-flight when retries timeout
                                    responseMessageTcpKiller.PauseConnectionOnNextResponse();
                                }

                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            portForwarder = clientAndTentacle.PortForwarder;

            var remotePath = Path.Combine(clientAndTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");
            var dataStream = DataStream.FromString("The Stream");

            // Start the script which will wait for a file to exist
            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.UploadFile(remotePath, dataStream, CancellationToken);

            Func<Task> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            fileTransferServiceCallCounts.UploadFileCallCountStarted.Should().BeGreaterOrEqualTo(2);
            fileTransferServiceCallCounts.DownloadFileCallCountStarted.Should().Be(0);

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(14));
        }

        [Test]
        [TestCase(TentacleType.Polling, false)] // Timeout an in-flight request
        [TestCase(TentacleType.Polling, true)] // Timeout trying to connect
        [TestCase(TentacleType.Listening, false)] // Timeout an in-flight request
        [TestCase(TentacleType.Listening, true)] // Timeout trying to connect
        public async Task WhenRpcRetriesTimeOut_DuringDownloadFile_TheRpcCallIsCancelled(TentacleType tentacleType, bool stopPortForwarderAfterFirstCall)
        {
            PortForwarder portForwarder = null!;
            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToFileTransferService()
                    .CountCallsToFileTransferService(out var fileTransferServiceCallCounts)
                    .RecordExceptionThrownInFileTransferService(out var fileTransferServiceException)
                    .DecorateFileTransferServiceWith(d => d
                        .BeforeDownloadFile(async (service, _) =>
                        {
                            await service.EnsureTentacleIsConnectedToServer(Logger);

                            // Kill the first DownloadFile call to force the rpc call into retries
                            if (fileTransferServiceException.DownloadFileLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                if (stopPortForwarderAfterFirstCall)
                                {
                                    // Kill the port forwarder so the next requests are in the connecting state when retries timeout
                                    Logger.Information("Killing PortForwarder");
                                    portForwarder!.Dispose();
                                }
                                else
                                {
                                    // Pause the port forwarder so the next requests are in-flight when retries timeout
                                    responseMessageTcpKiller.PauseConnectionOnNextResponse();
                                }

                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            portForwarder = clientAndTentacle.PortForwarder;

            using var tempFile = new RandomTemporaryFileBuilder().Build();

            // Start the script which will wait for a file to exist
            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.DownloadFile(tempFile.File.FullName, CancellationToken);

            Func<Task<DataStream>> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            fileTransferServiceCallCounts.DownloadFileCallCountStarted.Should().BeGreaterOrEqualTo(2);
            fileTransferServiceCallCounts.UploadFileCallCountStarted.Should().Be(0);

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(14));
        }
    }
}