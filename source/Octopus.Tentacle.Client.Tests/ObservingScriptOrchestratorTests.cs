using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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

            using var cancelOnceRunningIsObserved = new CancellationTokenSource();
            var scriptExecutor = new ScriptExecutorThatNeverCompletesCancellation(startContext, cancelOnceRunningIsObserved);

            var backoffStrategy = new StaticBackoffStrategy(TimeSpan.FromMilliseconds(5));

            var logger = new InMemoryLog();
            var tentacleClientObserver = new TestTentacleClientObserver();

            var orchestrator = CreateOrchestrator(scriptExecutor, backoffStrategy, tentacleClientObserver);

            var command = new ExecuteShellScriptCommandBuilder("task-1", ScriptIsolationLevel.NoIsolation)
                .WithScriptTicket(startContext.ScriptTicket)
                .Build();

            var scriptCancellationTimeoutBeforeAbandoning = TimeSpan.FromMilliseconds(100);

            // Act
            Func<Task> act = () => orchestrator.ExecuteScript(
                command,
                scriptCancellationTimeoutBeforeAbandoning,
                logger,
                cancelOnceRunningIsObserved.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

            logger.GetLogsForCategory(LogCategory.Warning).Should().Contain(m => m != null && m.Contains("Unable to cancel"));

            // It should have kept retrying cancellation (more than once) rather than giving up immediately.
            scriptExecutor.CancelScriptCallCount.Should().BeGreaterThan(1);

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

            using var cancelOnceRunningIsObserved = new CancellationTokenSource();
            var scriptExecutor = new ScriptExecutorThatCompletesCancellationOnSecondAttempt(startContext, cancelOnceRunningIsObserved);

            var backoffStrategy = new StaticBackoffStrategy(TimeSpan.FromMilliseconds(5));

            var logger = new InMemoryLog();
            var tentacleClientObserver = new TestTentacleClientObserver();

            var orchestrator = CreateOrchestrator(scriptExecutor, backoffStrategy, tentacleClientObserver);

            var command = new ExecuteShellScriptCommandBuilder("task-1", ScriptIsolationLevel.NoIsolation)
                .WithScriptTicket(startContext.ScriptTicket)
                .Build();

            // Act
            Func<Task> act = () => orchestrator.ExecuteScript(
                command,
                TimeSpan.FromSeconds(30),
                logger,
                cancelOnceRunningIsObserved.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

            logger.GetLogsForCategory(LogCategory.Warning).Should().BeEmpty();

            tentacleClientObserver.ScriptCancellationTimedOutEvents.Should().BeEmpty();
        }

        class ScriptExecutorThatNeverCompletesCancellation : IScriptExecutor
        {
            readonly ScriptOperationExecutionResult startResult;
            readonly CancellationTokenSource cancelOnceRunningIsObserved;

            public int CancelScriptCallCount { get; private set; }

            public ScriptExecutorThatNeverCompletesCancellation(CommandContext startContext, CancellationTokenSource cancelOnceRunningIsObserved)
            {
                startResult = RunningResult(startContext);
                this.cancelOnceRunningIsObserved = cancelOnceRunningIsObserved;
            }

            public Task<ScriptOperationExecutionResult> StartScript(ExecuteScriptCommand command, StartScriptIsBeingReAttempted startScriptIsBeingReAttempted, CancellationToken scriptExecutionCancellationToken)
                => Task.FromResult(startResult);

            public Task<ScriptOperationExecutionResult> GetStatus(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken)
            {
                // Once we've observed the script is actually running, request cancellation.
                cancelOnceRunningIsObserved.Cancel();

                return Task.FromResult(RunningResult(commandContext));
            }

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
            readonly CancellationTokenSource cancelOnceRunningIsObserved;
            int cancelScriptCallCount;

            public ScriptExecutorThatCompletesCancellationOnSecondAttempt(CommandContext startContext, CancellationTokenSource cancelOnceRunningIsObserved)
            {
                startResult = RunningResult(startContext);
                this.cancelOnceRunningIsObserved = cancelOnceRunningIsObserved;
            }

            public Task<ScriptOperationExecutionResult> StartScript(ExecuteScriptCommand command, StartScriptIsBeingReAttempted startScriptIsBeingReAttempted, CancellationToken scriptExecutionCancellationToken)
                => Task.FromResult(startResult);

            public Task<ScriptOperationExecutionResult> GetStatus(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken)
            {
                // Once we've observed the script is actually running, request cancellation.
                cancelOnceRunningIsObserved.Cancel();

                return Task.FromResult(RunningResult(commandContext));
            }

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

        class StaticBackoffStrategy : IScriptObserverBackoffStrategy
        {
            readonly TimeSpan backoff;

            public StaticBackoffStrategy(TimeSpan backoff)
            {
                this.backoff = backoff;
            }

            public TimeSpan GetBackoff(int iteration) => backoff;
        }
    }
}
