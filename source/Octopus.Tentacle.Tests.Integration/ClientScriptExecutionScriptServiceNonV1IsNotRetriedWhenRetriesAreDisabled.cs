﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionScriptServiceNonV1IsNotRetriedWhenRetriesAreDisabled : IntegrationTest
    {
        [Test]
        [TentacleConfigurations]
        public async Task WhenNetworkFailureOccurs_DuringGetCapabilities_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientCapabilitiesServiceV2>(out var capabilitiesRecordedUsages)
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var scriptRecordedUsages)
                    .DecorateCapabilitiesServiceV2With(d => d
                        .BeforeGetCapabilities(
                            async () =>
                            {
                                // Due to the GetCapabilities response getting cached, we must
                                // use a different service to ensure Tentacle is connected to Server.
                                // Otherwise, the response to the 'ensure connection' will get cached
                                // and any subsequent calls will succeed w/o using the network.
                                await tcpConnectionUtilities.EnsureConnectionIsSetupBeforeKillingIt();

                                if (capabilitiesRecordedUsages.For(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync)).LastException is null)
                                {
                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder().Print("hello")).Build();

            var logs = new List<ProcessOutput>();
            var executeScriptTask = clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken);

            var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ConnectionFaulted, tentacleConfigurationTestCase.TentacleType, clientTentacle).Build();

            await AssertionExtensions.Should(async () => await executeScriptTask).ThrowExceptionContractAsync(expectedException);

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("hello");

            var getCapabilitiesUsages = capabilitiesRecordedUsages.For(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync));
            getCapabilitiesUsages.LastException.Should().NotBeNull();
            getCapabilitiesUsages.Started.Should().Be(1);
            getCapabilitiesUsages.Completed.Should().Be(1);

            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().Be(0);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Completed.Should().Be(0);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(0);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Completed.Should().Be(0);
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
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .DecorateAllScriptServicesWith(u => u
                        .BeforeStartScript(
                            async () =>
                            {
                                await Task.CompletedTask;

                                if (recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).LastException is null)
                                {
                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .Print("AllDone"))
                .Build();

            var logs = new List<ProcessOutput>();
            var executeScriptTask = clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken);

            var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ConnectionFaulted, tentacleConfigurationTestCase.TentacleType, clientTentacle).Build();

            await AssertionExtensions.Should(async () => await executeScriptTask).ThrowExceptionContractAsync(expectedException);

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).LastException.Should().NotBeNull();
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().Be(0);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);

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
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .DecorateAllScriptServicesWith(u => u
                        .BeforeGetStatus(
                            async () =>
                            {
                                await Task.CompletedTask;

                                if (recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).LastException is null)
                                {
                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();

            var logs = new List<ProcessOutput>();

            var executeScriptTask = clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken);

            var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ConnectionFaulted, tentacleConfigurationTestCase.TentacleType, clientTentacle).Build();

            await AssertionExtensions.Should(async () => await executeScriptTask).ThrowExceptionContractAsync(expectedException);

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).LastException.Should().NotBeNull();
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);

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
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .DecorateAllScriptServicesWith(u => u
                        .BeforeGetStatus(
                            async () =>
                            {
                                await Task.CompletedTask;

                                cts.Cancel();
                            })
                        .BeforeCancelScript(
                            async () =>
                            {
                                if (recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).LastException is null)
                                {
                                    await tcpConnectionUtilities.EnsureConnectionIsSetupBeforeKillingIt();
                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();

            var logs = new List<ProcessOutput>();
            var executeScriptTask = clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, cts.Token);

            var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ConnectionFaulted, tentacleConfigurationTestCase.TentacleType, clientTentacle).Build();

            await AssertionExtensions.Should(async () => await executeScriptTask).ThrowExceptionContractAsync(expectedException);

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).LastException.Should().NotBeNull();
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);

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
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .DecorateAllScriptServicesWith(u => u
                        .BeforeCompleteScript(
                            async () =>
                            {
                                await Task.CompletedTask;

                                if (recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).LastException is null)
                                {
                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .Print("AllDone"))
                .Build();

            var logs = new List<ProcessOutput>();
            await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("AllDone");

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).LastException.Should().NotBeNull();
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(1);
        }
    }
}