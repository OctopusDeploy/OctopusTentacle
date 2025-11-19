using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    public class ScriptServiceV1ExecutorTests
    {
        /// <summary>
        /// In ScriptServiceV1, it is possible that additional logs may be returned after the
        /// first call to GetStatus responds with a completed status. The script executor
        /// mitigates against this by checking a second time if the script is completed.
        /// </summary>
        [Test]
        public async Task GetStatusWillDoubleCheckIfProcessIsCompleted()
        {
            // Arrange
            var scriptService = Substitute.For<IAsyncClientScriptService>();
            scriptService.GetStatusAsync(Arg.Any<ScriptStatusRequest>(), Arg.Any<HalibutProxyRequestOptions>())
                .Returns(
                    x => Task.FromResult(new ScriptStatusResponse(
                        x.Arg<ScriptStatusRequest>().Ticket,
                        ProcessState.Complete,
                        nextLogSequence: 1,
                        exitCode: 0,
                        logs: new List<ProcessOutput>
                        {
                            new(ProcessOutputSource.StdOut, "First log line"),
                        })),
                    x => Task.FromResult(new ScriptStatusResponse(
                        x.Arg<ScriptStatusRequest>().Ticket,
                        ProcessState.Complete,
                        nextLogSequence: 2,
                        exitCode: 0,
                        logs: new List<ProcessOutput>
                        {
                            new(ProcessOutputSource.StdOut, "Second log line"),
                        }))
                );

            var scriptExecutor = new ScriptServiceV1Executor(
                scriptService,
                RpcCallExecutorFactory.Create(TimeSpan.Zero, Substitute.For<ITentacleClientObserver>()),
                ClientOperationMetricsBuilder.Start(),
                Substitute.For<ITentacleClientTaskLog>()
            );


            var context = new CommandContext(
                scriptTicket: new ScriptTicket("TestTicket"),
                nextLogSequence: 0,
                ScriptServiceVersion.ScriptServiceVersion1
            );

            // Act
            var result = await scriptExecutor.GetStatus(context, CancellationToken.None);

            // Assert
            result.ScriptStatus.Should().NotBeNull();
            result.ScriptStatus.ExitCode.Should().Be(0);
            result.ScriptStatus.Logs.Should().HaveCount(2);
            result.ScriptStatus.Logs[0].Source.Should().Be(ProcessOutputSource.StdOut);
            result.ScriptStatus.Logs[0].Text.Should().Be("First log line");
            result.ScriptStatus.Logs[1].Source.Should().Be(ProcessOutputSource.StdOut);
            result.ScriptStatus.Logs[1].Text.Should().Be("Second log line");
            result.ScriptStatus.State.Should().Be(ProcessState.Complete);
        }

        [Test]
        public async Task GetStatusWillOnlyCheckOnceIfProcessIsNotComplete()
        {
            // Arrange
            var scriptService = Substitute.For<IAsyncClientScriptService>();
            scriptService.GetStatusAsync(Arg.Any<ScriptStatusRequest>(), Arg.Any<HalibutProxyRequestOptions>())
                .Returns(
                    x => Task.FromResult(
                        new ScriptStatusResponse(
                            x.Arg<ScriptStatusRequest>().Ticket,
                            ProcessState.Running,
                            nextLogSequence: 1,
                            exitCode: 0,
                            logs: new List<ProcessOutput>
                            {
                                new(ProcessOutputSource.StdOut, "First log line"),
                            }
                        )
                    ),
                    x => Task.FromResult(
                        new ScriptStatusResponse(
                            x.Arg<ScriptStatusRequest>().Ticket,
                            ProcessState.Complete,
                            nextLogSequence: 2,
                            exitCode: 0,
                            logs: new List<ProcessOutput>
                            {
                                new(ProcessOutputSource.StdOut, "This line should not be returned"),
                            }
                        )
                    )
                );
            var scriptExecutor = new ScriptServiceV1Executor(
                scriptService,
                RpcCallExecutorFactory.Create(TimeSpan.Zero, Substitute.For<ITentacleClientObserver>()),
                ClientOperationMetricsBuilder.Start(),
                Substitute.For<ITentacleClientTaskLog>()
            );
            var context = new CommandContext(
                scriptTicket: new ScriptTicket("TestTicket"),
                nextLogSequence: 0,
                scripServiceVersionUsed: ScriptServiceVersion.ScriptServiceVersion1
            );

            // Act
            var result = await scriptExecutor.GetStatus(context, CancellationToken.None);

            // Assert
            result.ScriptStatus.Should().NotBeNull();
            result.ScriptStatus.ExitCode.Should().Be(0);
            result.ScriptStatus.Logs.Should().HaveCount(1);
            result.ScriptStatus.Logs[0].Source.Should().Be(ProcessOutputSource.StdOut);
            result.ScriptStatus.Logs[0].Text.Should().Be("First log line");
            result.ScriptStatus.State.Should().Be(ProcessState.Running);
        }
    }
}
