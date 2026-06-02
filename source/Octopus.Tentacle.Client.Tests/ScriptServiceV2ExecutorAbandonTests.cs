using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    public class ScriptServiceV2ExecutorAbandonTests
    {
        static ScriptServiceV2Executor CreateExecutor(IAsyncClientScriptServiceV2 scriptService)
            => new(
                scriptService,
                RpcCallExecutorFactory.Create(TimeSpan.Zero, Substitute.For<ITentacleClientObserver>()),
                ClientOperationMetricsBuilder.Start(),
                TimeSpan.Zero,
                new TentacleClientOptions(new RpcRetrySettings(RetriesEnabled: false, RetryDuration: TimeSpan.Zero)),
                Substitute.For<ITentacleClientTaskLog>());

        static CommandContext Context() => new(new ScriptTicket("TestTicket"), 0, ScriptServiceVersion.ScriptServiceVersion2);

        [Test]
        public async Task AbandonScript_CallsAbandonScriptAsync()
        {
            var scriptService = Substitute.For<IAsyncClientScriptServiceV2>();
            scriptService.AbandonScriptAsync(Arg.Any<AbandonScriptCommandV2>(), Arg.Any<HalibutProxyRequestOptions>())
                .Returns(x => Task.FromResult(new ScriptStatusResponseV2(
                    x.Arg<AbandonScriptCommandV2>().Ticket, ProcessState.Complete,
                    ScriptExitCodes.AbandonedExitCode, new List<ProcessOutput>(), 1)));

            var executor = CreateExecutor(scriptService);

            var result = await executor.AbandonScript(Context());

            await scriptService.Received(1).AbandonScriptAsync(Arg.Any<AbandonScriptCommandV2>(), Arg.Any<HalibutProxyRequestOptions>());
            await scriptService.DidNotReceive().CancelScriptAsync(Arg.Any<CancelScriptCommandV2>(), Arg.Any<HalibutProxyRequestOptions>());
            result.ScriptStatus.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);
        }
    }
}
