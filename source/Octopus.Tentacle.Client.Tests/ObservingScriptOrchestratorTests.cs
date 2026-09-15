using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    public class ObservingScriptOrchestratorTests
    {
        static ScriptOperationExecutionResult RunningResult(CommandContext context)
            => new(new ScriptStatus(ProcessState.Running, 0, new()), context);

        static ScriptOperationExecutionResult CompleteResult(CommandContext context)
            => new(new ScriptStatus(ProcessState.Complete, 0, new()), context);

        ObservingScriptOrchestrator CreateOrchestrator(IScriptExecutor scriptExecutor, IScriptObserverBackoffStrategy backoffStrategy, ITentacleClientObserver tentacleClientObserver)
            => new(
                backoffStrategy,
                _ => { },
                _ => Task.CompletedTask,
                scriptExecutor,
                tentacleClientObserver);

        [Test]
        public async Task WhenCancellationCannotBeCompletedWithinTheTimeout_ItLogsAWarningAndAbandonsObserving()
        {
            // Arrange
            var startContext = new CommandContext(new ScriptTicket("ticket"), 0, ScriptServiceVersion.ScriptServiceVersion1);

            var scriptExecutor = new ScriptExecutorThatNeverCompletesCancellation(startContext);

            var backoffStrategy = Substitute.For<IScriptObserverBackoffStrategy>();
            backoffStrategy.GetBackoff(Arg.Any<int>()).Returns(TimeSpan.FromMilliseconds(5));

            var logger = new InMemoryLog();
            var tentacleClientObserver = new TestTentacleClientObserver();

            var orchestrator = CreateOrchestrator(scriptExecutor, backoffStrategy, tentacleClientObserver);

            var command = new ExecuteShellScriptCommandBuilder("task-1", ScriptIsolationLevel.NoIsolation)
                .WithScriptTicket(startContext.ScriptTicket)
                .Build();

            using var alreadyCancelled = new CancellationTokenSource();
            alreadyCancelled.Cancel();

            var scriptCancellationTimeoutBeforeAbandoning = TimeSpan.FromMilliseconds(50);

            // Act
            Func<Task> act = () => orchestrator.ExecuteScript(
                command,
                scriptCancellationTimeoutBeforeAbandoning,
                logger,
                alreadyCancelled.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

            logger.GetLogsForCategory(LogCategory.Warning).Should().Contain(m => m != null && m.Contains("Unable to cancel"));

            // It should have kept retrying cancellation (more than once) rather than giving up immediately.
            scriptExecutor.CancelScriptCallCount.Should().BeGreaterThan(0);

            tentacleClientObserver.ScriptCancellationTimedOutEvents.Should().ContainSingle(e =>
                e.ScriptTicket == startContext.ScriptTicket &&
                e.TaskId == command.TaskId &&
                e.IsolationLevel == command.IsolationConfiguration.IsolationLevel &&
                e.MutexName == command.IsolationConfiguration.MutexName &&
                e.ScriptCancellationTimeoutBeforeAbandoning == scriptCancellationTimeoutBeforeAbandoning);
        }

        [Test]
        public async Task WhenCancellationCompletesBeforeTheTimeoutElapses_ItDoesNotLogAWarning()
        {
            // Arrange
            var startContext = new CommandContext(new ScriptTicket("ticket"), 0, ScriptServiceVersion.ScriptServiceVersion1);

            // First cancellation attempt is still running, second attempt reports complete.
            var scriptExecutor = new ScriptExecutorThatCompletesCancellationOnSecondAttempt(startContext);

            var backoffStrategy = Substitute.For<IScriptObserverBackoffStrategy>();
            backoffStrategy.GetBackoff(Arg.Any<int>()).Returns(TimeSpan.FromMilliseconds(5));

            var logger = new InMemoryLog();
            var tentacleClientObserver = new TestTentacleClientObserver();

            var orchestrator = CreateOrchestrator(scriptExecutor, backoffStrategy, tentacleClientObserver);

            var command = new ExecuteShellScriptCommandBuilder("task-1", ScriptIsolationLevel.NoIsolation)
                .WithScriptTicket(startContext.ScriptTicket)
                .Build();

            using var alreadyCancelled = new CancellationTokenSource();
            alreadyCancelled.Cancel();

            // Act
            Func<Task> act = () => orchestrator.ExecuteScript(
                command,
                TimeSpan.FromSeconds(30),
                logger,
                alreadyCancelled.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

            logger.GetLogsForCategory(LogCategory.Warning).Should().BeEmpty();

            tentacleClientObserver.ScriptCancellationTimedOutEvents.Should().BeEmpty();
        }

        class ScriptExecutorThatNeverCompletesCancellation : IScriptExecutor
        {
            readonly ScriptOperationExecutionResult startResult;

            public int CancelScriptCallCount { get; private set; }

            public ScriptExecutorThatNeverCompletesCancellation(CommandContext startContext)
            {
                startResult = RunningResult(startContext);
            }

            public Task<ScriptOperationExecutionResult> StartScript(ExecuteScriptCommand command, StartScriptIsBeingReAttempted startScriptIsBeingReAttempted, CancellationToken scriptExecutionCancellationToken)
                => Task.FromResult(startResult);

            public Task<ScriptOperationExecutionResult> GetStatus(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken)
                => throw new NotSupportedException("Not expected to be called by this test.");

            public Task<ScriptOperationExecutionResult> CancelScript(CommandContext commandContext)
            {
                CancelScriptCallCount++;

                // Cancellation never succeeds - the tentacle keeps reporting the script as still running.
                return Task.FromResult(RunningResult(commandContext));
            }

            public Task<ScriptStatus?> CompleteScript(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken)
                => throw new NotSupportedException("Not expected to be called by this test.");
        }

        class ScriptExecutorThatCompletesCancellationOnSecondAttempt : IScriptExecutor
        {
            readonly ScriptOperationExecutionResult startResult;
            int cancelScriptCallCount;

            public ScriptExecutorThatCompletesCancellationOnSecondAttempt(CommandContext startContext)
            {
                startResult = RunningResult(startContext);
            }

            public Task<ScriptOperationExecutionResult> StartScript(ExecuteScriptCommand command, StartScriptIsBeingReAttempted startScriptIsBeingReAttempted, CancellationToken scriptExecutionCancellationToken)
                => Task.FromResult(startResult);

            public Task<ScriptOperationExecutionResult> GetStatus(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken)
                => throw new NotSupportedException("Not expected to be called by this test.");

            public Task<ScriptOperationExecutionResult> CancelScript(CommandContext commandContext)
            {
                cancelScriptCallCount++;

                var result = cancelScriptCallCount == 1
                    ? RunningResult(commandContext)
                    : CompleteResult(commandContext);

                return Task.FromResult(result);
            }

            public Task<ScriptStatus?> CompleteScript(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken)
                => Task.FromResult((ScriptStatus?)null);
        }
    }
}
