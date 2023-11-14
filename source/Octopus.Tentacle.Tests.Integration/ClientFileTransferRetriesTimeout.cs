using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
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
        readonly TimeSpan retryIfRemainingDurationAtLeastBuffer = TimeSpan.FromSeconds(1);
        readonly TimeSpan retryBackoffBuffer = TimeSpan.FromSeconds(2);

        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] { typeof(StopPortForwarderAfterFirstCallValues) })]
        public async Task WhenRpcRetriesTimeOut_DuringUploadFile_TheRpcCallIsCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, bool stopPortForwarderAfterFirstCall)
        {
            PortForwarder portForwarder = null!;
            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var methodUsages)
                    .HookServiceMethod<IAsyncClientFileTransferService>(nameof(IAsyncClientFileTransferService.UploadFileAsync),
                        async (_,_) =>
                        {
                            await tcpConnectionUtilities.RestartTcpConnection();

                            if (methodUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).LastException is null)
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
                .Build(CancellationToken);

            portForwarder = clientAndTentacle.PortForwarder;

            var inMemoryLog = new InMemoryLog();

            var remotePath = Path.Combine(clientAndTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");
            var dataStream = DataStream.FromString("The Stream");

            // Start the script which will wait for a file to exist
            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.UploadFile(remotePath, dataStream, CancellationToken, inMemoryLog);

            Func<Task> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            methodUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Started.Should().BeGreaterOrEqualTo(2);
            methodUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Started.Should().Be(0);

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(clientAndTentacle.RpcRetrySettings.RetryDuration - retryIfRemainingDurationAtLeastBuffer - retryBackoffBuffer);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndRetryFailure();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenUploadFileFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var retryDuration = TimeSpan.FromSeconds(15);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(retryDuration)
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var methodUsages)
                    .HookServiceMethod<IAsyncClientFileTransferService>(nameof(IAsyncClientFileTransferService.UploadFileAsync),
                        async (_,_) =>
                        {
                            await tcpConnectionUtilities.RestartTcpConnection();

                            // Sleep to make the initial RPC call take longer than the allowed retry duration
                            await Task.Delay(retryDuration + TimeSpan.FromSeconds(1));

                            // Kill the first UploadFile call to force the rpc call into retries
                            responseMessageTcpKiller.KillConnectionOnNextResponse();
                        })
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();
            var remotePath = Path.Combine(clientAndTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");
            var dataStream = DataStream.FromString("The Stream");
            var executeScriptTask = clientAndTentacle.TentacleClient.UploadFile(remotePath, dataStream, CancellationToken, inMemoryLog);

            Func<Task> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();

            methodUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Started.Should().Be(1);
            methodUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Started.Should().Be(0);

            inMemoryLog.ShouldHaveLoggedRetryFailureAndNoRetryAttempts();
        }

        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] { typeof(StopPortForwarderAfterFirstCallValues) })]
        public async Task WhenRpcRetriesTimeOut_DuringDownloadFile_TheRpcCallIsCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, bool stopPortForwarderAfterFirstCall)
        {
            PortForwarder portForwarder = null!;
            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var tracingStats)
                    .HookServiceMethod<IAsyncClientFileTransferService>(nameof(IAsyncClientFileTransferService.DownloadFileAsync),
                        async (_,_) =>
                        {
                            await tcpConnectionUtilities.RestartTcpConnection();

                            if (tracingStats.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).LastException is null)
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
                .Build(CancellationToken);

            portForwarder = clientAndTentacle.PortForwarder;

            using var tempFile = new RandomTemporaryFileBuilder().Build();

            var inMemoryLog = new InMemoryLog();

            // Start the script which will wait for a file to exist
            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.DownloadFile(tempFile.File.FullName, CancellationToken, inMemoryLog);

            Func<Task<DataStream>> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            tracingStats.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Started.Should().BeGreaterOrEqualTo(2);
            tracingStats.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Started.Should().Be(0);

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(clientAndTentacle.RpcRetrySettings.RetryDuration - retryIfRemainingDurationAtLeastBuffer - retryBackoffBuffer);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndRetryFailure();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenDownloadFileFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var retryDuration = TimeSpan.FromSeconds(15);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(retryDuration)
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var recordedUsages)
                    .HookServiceMethod<IAsyncClientFileTransferService>(nameof(IAsyncClientFileTransferService.DownloadFileAsync),
                        async (_,_) =>
                        {
                            await tcpConnectionUtilities.RestartTcpConnection();

                            // Sleep to make the initial RPC call take longer than the allowed retry duration
                            await Task.Delay(retryDuration + TimeSpan.FromSeconds(1));

                            // Kill the first DownloadFile call to force the rpc call into retries
                            responseMessageTcpKiller.KillConnectionOnNextResponse();
                        })
                    .Build())
                .Build(CancellationToken);

            using var tempFile = new RandomTemporaryFileBuilder().Build();
            var inMemoryLog = new InMemoryLog();
            var executeScriptTask = clientAndTentacle.TentacleClient.DownloadFile(tempFile.File.FullName, CancellationToken, inMemoryLog);

            Func<Task<DataStream>> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();

            recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Started.Should().Be(0);

            inMemoryLog.ShouldHaveLoggedRetryFailureAndNoRetryAttempts();
        }

        class StopPortForwarderAfterFirstCallValues : IEnumerable
        {
            public IEnumerator GetEnumerator()
            {
                yield return true;
                yield return false;
            }
        }
    }
}