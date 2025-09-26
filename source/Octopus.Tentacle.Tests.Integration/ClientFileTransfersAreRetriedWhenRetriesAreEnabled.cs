using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ClientFileTransfersAreRetriedWhenRetriesAreEnabled : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testCommonVersions: true, scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task FailedUploadsAreRetriedAndIsEventuallySuccessful(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var recordedUsages)
                    .DecorateFileTransferServiceWith(d => d
                        .BeforeUploadFile(
                            async () =>
                            {
                                await tcpConnectionUtilities.EnsureConnectionIsSetupBeforeKillingIt();

                                // Only kill the connection the first time, causing the upload
                                // to succeed - and therefore failing the test - if retries are attempted
                                if (recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).LastException is null)
                                {
                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            var res = await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken, inMemoryLog);
            res.Length.Should().Be(5);

            recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).LastException.Should().NotBeNull();
            recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Started.Should().Be(2);

            var downloadFile = await clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken);
            var actuallySent = await downloadFile.GetUtf8String(CancellationToken);
            actuallySent.Should().Be("Hello");

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }
        
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task LongRunningFileUploadsToTentacleAreRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // 100MB file which well exceeds any networking buffers, making it easy to control what is happening over the network.
            var fileSize = 1024*1024*100;
            bool hasSlept = false;
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithRetryDuration(TimeSpan.FromSeconds(1))
                .WithMinimumAttemptsForInterruptedLongRunningCalls(5)
                .WithPortForwarder(out var portForwarder)
                .WithByteTransferTracker(bytesTransferredCallback: (ClientToTentacleBytes, _, _) =>
                {
                    // Once about 30MB has been sent over the network, we are sure this is the file upload.
                    // which is what we want to interrupt.
                    if (ClientToTentacleBytes > fileSize / 3 && !hasSlept)
                    {
                        // Exceed the RPC retry duration
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                        // Terminate the file upload.
                        portForwarder.Value.EnterKillNewAndExistingConnectionsMode();
                        portForwarder.Value.ReturnToNormalMode();
                        hasSlept = true;
                    }       
                })
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var recordedUsages)
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            
            var res = await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString(new string('a', fileSize)), CancellationToken, inMemoryLog);
            res.Length.Should().Be(fileSize);

            recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).LastException.Should().NotBeNull();
            recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Started.Should().BeGreaterOrEqualTo(2);

            var downloadFile = await clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken);
            var actuallySent = await downloadFile.GetUtf8String(CancellationToken);
            actuallySent.Length.Should().Be(fileSize);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true, scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task FailedDownloadsAreRetriedAndIsEventuallySuccessful(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var recordedUsages)
                    .DecorateFileTransferServiceWith(d => d
                        .BeforeDownloadFile(
                            async () =>
                            {
                                await tcpConnectionUtilities.EnsureConnectionIsSetupBeforeKillingIt();

                                // Only kill the connection the first time, causing the upload
                                // to succeed - and therefore failing the test - if retries are attempted
                                if (recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).LastException is null)
                                {
                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);
            var downloadFile = await clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken, inMemoryLog);
            var actuallySent = await downloadFile.GetUtf8String(CancellationToken);

            recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).LastException.Should().NotBeNull();
            recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Started.Should().Be(2);

            actuallySent.Should().Be("Hello");

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }
    }
}
