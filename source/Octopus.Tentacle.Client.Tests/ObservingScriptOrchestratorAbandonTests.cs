using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    public class ObservingScriptOrchestratorAbandonTests
    {
        static CommandContext Context() => new(new ScriptTicket("T"), 0, ScriptServiceVersion.ScriptServiceVersion2);
        static CommandContext V1Context() => new(new ScriptTicket("T"), 0, ScriptServiceVersion.ScriptServiceVersion1);

        static ScriptOperationExecutionResult Running()
            => new(new ScriptStatus(ProcessState.Running, 0, new List<ProcessOutput>()), Context());

        static ScriptOperationExecutionResult Complete(int exitCode)
            => new(new ScriptStatus(ProcessState.Complete, exitCode, new List<ProcessOutput>()), Context());

        static ScriptOperationExecutionResult RunningV1()
            => new(new ScriptStatus(ProcessState.Running, 0, new List<ProcessOutput>()), V1Context());

        static ScriptOperationExecutionResult CompleteV1(int exitCode)
            => new(new ScriptStatus(ProcessState.Complete, exitCode, new List<ProcessOutput>()), V1Context());

        static ObservingScriptOrchestrator CreateOrchestrator(IScriptExecutor executor, TimeSpan? abandonAfter)
            => new(
                new ImmediateBackoff(),
                _ => { },
                _ => Task.CompletedTask,
                executor,
                abandonAfter);

        sealed class ImmediateBackoff : IScriptObserverBackoffStrategy
        {
            public TimeSpan GetBackoff(int iteration) => TimeSpan.Zero;
        }

        [Test]
        public async Task ParamUnset_CancelsOnly_NeverAbandons()
        {
            var executor = Substitute.For<IScriptExecutor>();
            executor.CancelScript(Arg.Any<CommandContext>())
                .Returns(Running(), Complete(ScriptExitCodes.CanceledExitCode));

            var orchestrator = CreateOrchestrator(executor, abandonAfter: null);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await orchestrator.ObserveUntilComplete(Running(), cts.Token);

            await executor.DidNotReceive().AbandonScript(Arg.Any<CommandContext>());
            await executor.Received().CancelScript(Arg.Any<CommandContext>());
        }

        [Test]
        public async Task ThresholdCrossed_SwitchesFromCancelToAbandon()
        {
            var executor = Substitute.For<IScriptExecutor>();
            // abandonAfter = Zero means the threshold is crossed on the first cancelled iteration,
            // so the orchestrator abandons immediately. AbandonScript returns Complete to end the loop.
            executor.AbandonScript(Arg.Any<CommandContext>())
                .Returns(Complete(ScriptExitCodes.AbandonedExitCode));

            var orchestrator = CreateOrchestrator(executor, abandonAfter: TimeSpan.Zero);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await orchestrator.ObserveUntilComplete(Running(), cts.Token);

            await executor.Received().AbandonScript(Arg.Any<CommandContext>());
            result.ScriptStatus.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);
        }

        [Test]
        public async Task ThresholdCrossed_ButVersionCannotAbandon_KeepsCancelling()
        {
            // ScriptServiceV1 has no abandon verb, so even with the threshold crossed the orchestrator
            // must keep cancelling and never call AbandonScript.
            var executor = Substitute.For<IScriptExecutor>();
            executor.CancelScript(Arg.Any<CommandContext>())
                .Returns(RunningV1(), CompleteV1(ScriptExitCodes.CanceledExitCode));

            var orchestrator = CreateOrchestrator(executor, abandonAfter: TimeSpan.Zero);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await orchestrator.ObserveUntilComplete(RunningV1(), cts.Token);

            await executor.DidNotReceive().AbandonScript(Arg.Any<CommandContext>());
            await executor.Received().CancelScript(Arg.Any<CommandContext>());
        }
    }
}
