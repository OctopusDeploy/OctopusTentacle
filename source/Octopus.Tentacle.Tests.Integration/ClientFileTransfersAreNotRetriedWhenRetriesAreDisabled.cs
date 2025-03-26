using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientFileTransfersAreNotRetriedWhenRetriesAreDisabled : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testCommonVersions: true, scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task FailedUploadsAreNotRetriedAndFail(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var recordedUsages)
                    .DecorateFileTransferServiceWith(d => d
                        .BeforeUploadFile(
                            async () =>
                            {
                                await tcpConnectionUtilities.RestartTcpConnection();

                                // Only kill the connection the first time, causing the upload
                                // to succeed - and therefore failing the test - if retries are attempted
                                if (recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).LastException is null)
                                {
                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");
            var uploadFileTask = clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);

            var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ConnectionFaulted, tentacleConfigurationTestCase.TentacleType, clientTentacle).Build();

            await AssertionExtensions.Should(async () => await uploadFileTask).ThrowExceptionContractAsync(expectedException);

            recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).LastException.Should().NotBeNull();
            recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Started.Should().Be(1);
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true, scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task FailedDownloadsAreNotRetriedAndFail(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientFileTransferService>(out var recordedUsages)
                    .DecorateFileTransferServiceWith(d => d
                        .BeforeDownloadFile(
                            async () =>
                            {
                                await tcpConnectionUtilities.RestartTcpConnection();

                                // Only kill the connection the first time, causing the upload
                                // to succeed - and therefore failing the test - if retries are attempted
                                if (recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).LastException is null)
                                {
                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);
            var downloadFileTask = clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken);
            
            var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ConnectionFaulted, tentacleConfigurationTestCase.TentacleType, clientTentacle).Build();

            await AssertionExtensions.Should(async () => await downloadFileTask).ThrowExceptionContractAsync(expectedException);

            recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).LastException.Should().NotBeNull();
            recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Started.Should().Be(1);
        }
    }
}