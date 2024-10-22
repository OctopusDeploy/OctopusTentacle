// using System;
// using System.IO;
// using System.Threading.Tasks;
// using FluentAssertions;
// using Halibut;
// using NUnit.Framework;
// using Octopus.Tentacle.CommonTestUtils;
// using Octopus.Tentacle.CommonTestUtils.Diagnostics;
// using Octopus.Tentacle.Contracts.ClientServices;
// using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
// using Octopus.Tentacle.Tests.Integration.Support;
// using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
// using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
// using Octopus.Tentacle.Tests.Integration.Util.Builders;
// using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
// using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;
//
// namespace Octopus.Tentacle.Tests.Integration
// {
//     public class ClientFileTransfersAreRetriedWhenRetriesAreEnabled : IntegrationTest
//     {
//         [Test]
//         [TentacleConfigurations(testCommonVersions: true, scriptServiceToTest: ScriptServiceVersionToTest.None)]
//         [SkipOnEnvironmentsWithKnownPerformanceIssues("it relies on timing, which may be inconsistent within the environment")]
//         public async Task FailedUploadsAreRetriedAndIsEventuallySuccessful(TentacleConfigurationTestCase tentacleConfigurationTestCase)
//         {
//             await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
//                 .WithPortForwarderDataLogging()
//                 .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
//                 .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
//                 .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
//                     .RecordMethodUsages<IAsyncClientFileTransferService>(out var recordedUsages)
//                     .DecorateFileTransferServiceWith(d => d
//                         .BeforeUploadFile(
//                             async () =>
//                             {
//                                 await tcpConnectionUtilities.RestartTcpConnection();
//
//                                 // Only kill the connection the first time, causing the upload
//                                 // to succeed - and therefore failing the test - if retries are attempted
//                                 if (recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).LastException is null)
//                                 {
//                                     responseMessageTcpKiller.KillConnectionOnNextResponse();
//                                 }
//                             }))
//                     .Build())
//                 .Build(CancellationToken);
//
//             var inMemoryLog = new InMemoryLog();
//
//             var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");
//
//             var res = await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken, inMemoryLog);
//             res.Length.Should().Be(5);
//
//             recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).LastException.Should().NotBeNull();
//             recordedUsages.For(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Started.Should().Be(2);
//
//             var downloadFile = await clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken);
//             var actuallySent = await downloadFile.GetUtf8String(CancellationToken);
//             actuallySent.Should().Be("Hello");
//
//             inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
//         }
//
//         [Test]
//         [TentacleConfigurations(testCommonVersions: true, scriptServiceToTest: ScriptServiceVersionToTest.None)]
//         public async Task FailedDownloadsAreRetriedAndIsEventuallySuccessful(TentacleConfigurationTestCase tentacleConfigurationTestCase)
//         {
//             await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
//                 .WithPortForwarderDataLogging()
//                 .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
//                 .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
//                 .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
//                     .RecordMethodUsages<IAsyncClientFileTransferService>(out var recordedUsages)
//                     .DecorateFileTransferServiceWith(d => d
//                         .BeforeDownloadFile(
//                             async () =>
//                             {
//                                 await tcpConnectionUtilities.RestartTcpConnection();
//
//                                 // Only kill the connection the first time, causing the upload
//                                 // to succeed - and therefore failing the test - if retries are attempted
//                                 if (recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).LastException is null)
//                                 {
//                                     responseMessageTcpKiller.KillConnectionOnNextResponse();
//                                 }
//                             }))
//                     .Build())
//                 .Build(CancellationToken);
//
//             var inMemoryLog = new InMemoryLog();
//
//             var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");
//
//             await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);
//             var downloadFile = await clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken, inMemoryLog);
//             var actuallySent = await downloadFile.GetUtf8String(CancellationToken);
//
//             recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).LastException.Should().NotBeNull();
//             recordedUsages.For(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Started.Should().Be(2);
//
//             actuallySent.Should().Be("Hello");
//
//             inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
//         }
//     }
// }
