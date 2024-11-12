using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Client.Extensions;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
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
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None, additionalParameterTypes: new object[] { typeof(StopPortForwarderAfterFirstCallValues) })]
        public async Task WhenRpcRetriesTimeOut_DuringUploadFile_TheRpcCallIsCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, bool stopPortForwarderAfterFirstCall)
        {
            var driveInfos = DriveInfo.GetDrives().Where(d => d.IsReady);

            Logger.Information($"WhenRpcRetriesTimeOut_DuringUploadFile_TheRpcCallIsCancelled Available Disk space before starting: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            PortForwarder portForwarder = null!;
            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var methodUsages)
                    .DecorateFileTransferServiceWith(d => d
                        .BeforeUploadFile(
                            async () =>
                            {
                                if (methodUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).LastException is null)
                                {
                                    // Ensure there is an active connection so it can be killed correctly
                                    await tcpConnectionUtilities.RestartTcpConnection();
                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                                else
                                {
                                    if (stopPortForwarderAfterFirstCall)
                                    {
                                        // Kill the port forwarder so the next requests are in the connecting state when retries timeout
                                        Logger.Information("Killing PortForwarder");
                                        portForwarder!.EnterKillNewAndExistingConnectionsMode();
                                    }
                                    else
                                    {
                                        // Ensure there is an active connection so it can be killed correctly
                                        await tcpConnectionUtilities.RestartTcpConnection();
                                        // Pause the port forwarder so the next requests are in-flight when retries timeout
                                        responseMessageTcpKiller.PauseConnectionOnNextResponse();
                                    }
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            portForwarder = clientAndTentacle.PortForwarder;
            Logger.Information($"WhenRpcRetriesTimeOut_DuringUploadFile_TheRpcCallIsCancelled Available Disk space after clientAndTentacle.PortForwarder: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            var inMemoryLog = new InMemoryLog();

            var remotePath = Path.Combine(clientAndTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");
            var dataStream = DataStream.FromString("The Stream");

            // Start the script which will wait for a file to exist
            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.UploadFile(remotePath, dataStream, CancellationToken, inMemoryLog);
            
            Logger.Information($"WhenRpcRetriesTimeOut_DuringUploadFile_TheRpcCallIsCancelled Available Disk space after TentacleClient.UploadFile: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ConnectionFaulted, tentacleConfigurationTestCase.TentacleType, clientAndTentacle).Build();

            await AssertionExtensions.Should(async () => await executeScriptTask).ThrowExceptionContractAsync(expectedException);
            duration.Stop();

            methodUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Started.Should().BeGreaterOrEqualTo(2);
            methodUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Started.Should().Be(0);
            
            Logger.Information($"WhenRpcRetriesTimeOut_DuringUploadFile_TheRpcCallIsCancelled Available Disk space after assertion: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(clientAndTentacle.RpcRetrySettings.RetryDuration - retryIfRemainingDurationAtLeastBuffer - retryBackoffBuffer);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndRetryFailure();
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task WhenUploadFileFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {            
            
            var driveInfos = DriveInfo.GetDrives().Where(d => d.IsReady);

            Logger.Information($"WhenUploadFileFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut Available Disk space before starting: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            var retryDuration = TimeSpan.FromSeconds(15);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(retryDuration)
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var methodUsages)
                    .DecorateFileTransferServiceWith(d => d
                        .BeforeUploadFile(
                            async () =>
                            {
                                await tcpConnectionUtilities.RestartTcpConnection();

                                // Sleep to make the initial RPC call take longer than the allowed retry duration
                                await Task.Delay(retryDuration + TimeSpan.FromSeconds(1));

                                // Kill the first UploadFile call to force the rpc call into retries
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }))
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();
            var remotePath = Path.Combine(clientAndTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");
            var dataStream = DataStream.FromString("The Stream");
            var executeScriptTask = clientAndTentacle.TentacleClient.UploadFile(remotePath, dataStream, CancellationToken, inMemoryLog);

            var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ConnectionFaulted, tentacleConfigurationTestCase.TentacleType, clientAndTentacle).Build();
            Logger.Information($"WhenUploadFileFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut Available Disk space after expectedException: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            await AssertionExtensions.Should(async () => await executeScriptTask).ThrowExceptionContractAsync(expectedException);

            methodUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Started.Should().Be(1);
            methodUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Started.Should().Be(0);
            
            Logger.Information($"WhenUploadFileFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut Available Disk space after assertions: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            inMemoryLog.ShouldHaveLoggedRetryFailureAndNoRetryAttempts();
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None, additionalParameterTypes: new object[] { typeof(StopPortForwarderAfterFirstCallValues) })]
        public async Task WhenRpcRetriesTimeOut_DuringDownloadFile_TheRpcCallIsCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, bool stopPortForwarderAfterFirstCall)
        {
            var driveInfos = DriveInfo.GetDrives().Where(d => d.IsReady);

            Logger.Information($"WhenRpcRetriesTimeOut_DuringDownloadFile_TheRpcCallIsCancelled Available Disk space before starting: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            PortForwarder portForwarder = null!;
            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var recordedUsages)
                    .DecorateFileTransferServiceWith(d => d
                        .BeforeDownloadFile(
                            async () =>
                            {
                                if (recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).LastException is null)
                                {
                                    // Ensure there is an active connection so it can be killed correctly
                                    await tcpConnectionUtilities.RestartTcpConnection();
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
                                        // Ensure there is an active connection so it can be killed correctly
                                        await tcpConnectionUtilities.RestartTcpConnection();
                                        // Pause the port forwarder so the next requests are in-flight when retries timeout
                                        responseMessageTcpKiller.PauseConnectionOnNextResponse();
                                    }
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            portForwarder = clientAndTentacle.PortForwarder;
            Logger.Information($"WhenRpcRetriesTimeOut_DuringDownloadFile_TheRpcCallIsCancelled Available Disk space after CreateLegacyBuilder: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            using var tempFile = new RandomTemporaryFileBuilder().Build();
            Logger.Information($"WhenRpcRetriesTimeOut_DuringDownloadFile_TheRpcCallIsCancelled Available Disk space after RandomTemporaryFileBuilder: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            var inMemoryLog = new InMemoryLog();
            
            // Start the script which will wait for a file to exist
            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.DownloadFile(tempFile.File.FullName, CancellationToken, inMemoryLog);
            
            Logger.Information($"WhenRpcRetriesTimeOut_DuringDownloadFile_TheRpcCallIsCancelled Available Disk space after TentacleClient.DownloadFile: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ConnectionFaulted, tentacleConfigurationTestCase.TentacleType, clientAndTentacle).Build();

            await AssertionExtensions.Should(async () => await executeScriptTask).ThrowExceptionContractAsync(expectedException);

            duration.Stop();

            recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Started.Should().BeGreaterOrEqualTo(2);
            recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Started.Should().Be(0);
            
            Logger.Information($"WhenRpcRetriesTimeOut_DuringDownloadFile_TheRpcCallIsCancelled Available Disk space after recordedUsages: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(clientAndTentacle.RpcRetrySettings.RetryDuration - retryIfRemainingDurationAtLeastBuffer - retryBackoffBuffer);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndRetryFailure();
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task WhenDownloadFileFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var driveInfos = DriveInfo.GetDrives().Where(d => d.IsReady);

            Logger.Information($"WhenDownloadFileFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut Available Disk space before starting: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            var retryDuration = TimeSpan.FromSeconds(15);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(retryDuration)
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var recordedUsages)
                    .DecorateFileTransferServiceWith(d => d
                        .BeforeDownloadFile(
                            async () =>
                            {
                                await tcpConnectionUtilities.RestartTcpConnection();

                                // Sleep to make the initial RPC call take longer than the allowed retry duration
                                await Task.Delay(retryDuration + TimeSpan.FromSeconds(1));

                                // Kill the first DownloadFile call to force the rpc call into retries
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }))
                    .Build())
                .Build(CancellationToken);

            using var tempFile = new RandomTemporaryFileBuilder().Build();
            Logger.Information($"WhenDownloadFileFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut Available Disk space after RandomTemporaryFileBuilder: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");
            
            var inMemoryLog = new InMemoryLog();
            var executeScriptTask = clientAndTentacle.TentacleClient.DownloadFile(tempFile.File.FullName, CancellationToken, inMemoryLog);
            
            Logger.Information($"WhenRpcRetriesTimeOut_DuringDownloadFile_TheRpcCallIsCancelled Available Disk space after TentacleClient.DownloadFile: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            Func<Task<DataStream>> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();

            recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Started.Should().Be(0);
            
            Logger.Information($"WhenDownloadFileFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut Available Disk space after recordedUsages: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

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