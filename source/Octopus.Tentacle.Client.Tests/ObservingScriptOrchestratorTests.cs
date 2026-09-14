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
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    public class ObservingScriptOrchestratorTests
    {
        static ScriptOperationExecutionResult RunningResult(CommandContext context)
            => new(new ScriptStatus(ProcessState.Running, 0, new()), context);

        static ScriptOperationExecutionResult CompleteResult(CommandContext context)
            => new(new ScriptStatus(ProcessState.Complete, 0, new()), context);

        ObservingScriptOrchestrator CreateOrchestrator(IScriptExecutor scriptExecutor, IScriptObserverBackoffStrategy backoffStrategy)
            => new(
                backoffStrategy,
                _ => { },
                _ => Task.CompletedTask,
                scriptExecutor);

        [Test]
        public async Task WhenCancellationCannotBeCompletedWithinTheTimeout_ItLogsAWarningAndAbandonsObserving()
        {
            // Arrange
            var startContext = new CommandContext(new ScriptTicket("ticket"), 0, ScriptServiceVersion.ScriptServiceVersion1);

            var scriptExecutor = Substitute.For<IScriptExecutor>();
            scriptExecutor.StartScript(Arg.Any<ExecuteScriptCommand>(), Arg.Any<StartScriptIsBeingReAttempted>(), Arg.Any<CancellationToken>())
                .Returns(RunningResult(startContext));

            // Cancellation never succeeds - the tentacle keeps reporting the script as still running.
            scriptExecutor.CancelScript(Arg.Any<CommandContext>())
                .Returns(callInfo => RunningResult(callInfo.Arg<CommandContext>()));

            var backoffStrategy = Substitute.For<IScriptObserverBackoffStrategy>();
            backoffStrategy.GetBackoff(Arg.Any<int>()).Returns(TimeSpan.FromMilliseconds(5));

            var logger = Substitute.For<ITentacleClientTaskLog>();

            var orchestrator = CreateOrchestrator(scriptExecutor, backoffStrategy);

            var command = new ExecuteShellScriptCommandBuilder("task-1", ScriptIsolationLevel.NoIsolation)
                .WithScriptTicket(startContext.ScriptTicket)
                .Build();

            using var alreadyCancelled = new CancellationTokenSource();
            alreadyCancelled.Cancel();

            // Act
            Func<Task> act = () => orchestrator.ExecuteScript(
                command,
                TimeSpan.FromMilliseconds(50),
                logger,
                alreadyCancelled.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

            logger.Received().Warn(Arg.Is<string>(m => m.Contains("Unable to cancel")));

            // It should have kept retrying cancellation (more than once) rather than giving up immediately.
            _ = scriptExecutor.Received().CancelScript(Arg.Any<CommandContext>());
        }

        [Test]
        public async Task WhenCancellationCompletesBeforeTheTimeoutElapses_ItDoesNotLogAWarning()
        {
            // Arrange
            var startContext = new CommandContext(new ScriptTicket("ticket"), 0, ScriptServiceVersion.ScriptServiceVersion1);

            var scriptExecutor = Substitute.For<IScriptExecutor>();
            scriptExecutor.StartScript(Arg.Any<ExecuteScriptCommand>(), Arg.Any<StartScriptIsBeingReAttempted>(), Arg.Any<CancellationToken>())
                .Returns(RunningResult(startContext));

            // First cancellation attempt is still running, second attempt reports complete.
            scriptExecutor.CancelScript(Arg.Any<CommandContext>())
                .Returns(
                    callInfo => RunningResult(callInfo.Arg<CommandContext>()),
                    callInfo => CompleteResult(callInfo.Arg<CommandContext>()));

            scriptExecutor.CompleteScript(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>())
                .Returns((ScriptStatus?)null);

            var backoffStrategy = Substitute.For<IScriptObserverBackoffStrategy>();
            backoffStrategy.GetBackoff(Arg.Any<int>()).Returns(TimeSpan.FromMilliseconds(5));

            var logger = Substitute.For<ITentacleClientTaskLog>();

            var orchestrator = CreateOrchestrator(scriptExecutor, backoffStrategy);

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

            logger.DidNotReceive().Warn(Arg.Any<string>());
        }
    }
}
