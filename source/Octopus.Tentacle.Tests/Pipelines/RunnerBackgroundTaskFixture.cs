#if PIPELINES_RUNNER
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client.Model;
using Octopus.Pipelines.Runner.Contracts;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Tentacle.Background;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Pipelines;

namespace Octopus.Tentacle.Tests.Pipelines
{
    [TestFixture]
    public class RunnerBackgroundTaskFixture
    {
        static int ClosedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        static ITentacleConfiguration Configuration(params OctopusServerConfiguration[] servers)
        {
            var configuration = Substitute.For<ITentacleConfiguration>();
            configuration.TrustedOctopusServers.Returns(servers);
#pragma warning disable CS0618
            configuration.TentacleSquid.Returns("SQ-TEST");
#pragma warning restore CS0618
            return configuration;
        }

        static ITentacleConfiguration PollingConfiguration()
            => Configuration(new OctopusServerConfiguration("ABCDEF")
            {
                CommunicationStyle = CommunicationStyle.TentacleActive,
                Address = new Uri($"https://127.0.0.1:{ClosedPort()}")
            });

        static RunnerBackgroundTask NewTask(ITentacleConfiguration configuration, ISystemLog log, TimeSpan? stopTimeout = null, IJobBackend? backend = null, ILogStore? logStore = null)
            => new RunnerBackgroundTask(configuration, log, () => false, () => false, stopTimeout, backend, logStore);

        [Test]
        public void StartDoesNothingWhenSettingsSaySkip()
        {
            var log = Substitute.For<ISystemLog>();
            var configuration = Configuration(new OctopusServerConfiguration("A")
            {
                CommunicationStyle = CommunicationStyle.TentaclePassive,
                Address = new Uri("https://listening:10933")
            });
            var task = NewTask(configuration, log);

            task.Start();

            log.Received(1).Info(Arg.Is<string>(s => s.Contains("polling")));
            task.Executor.Should().BeNull();
            task.Invoking(t => t.Stop()).Should().NotThrow();
        }

        [Test]
        public void StartSwallowsFailures()
        {
            var log = Substitute.For<ISystemLog>();
            var configuration = Substitute.For<ITentacleConfiguration>();
            configuration.TrustedOctopusServers.Returns(_ => throw new InvalidOperationException("boom"));
            var task = NewTask(configuration, log);

            task.Invoking(t => t.Start()).Should().NotThrow();

            log.Received().Error(Arg.Any<Exception>(), Arg.Any<string>());
            task.Executor.Should().BeNull();
            task.Invoking(t => t.Stop()).Should().NotThrow();
        }

        [Test]
        public void StartsAndStopsAgainstAnUnreachableServer()
        {
            var log = Substitute.For<ISystemLog>();
            var task = NewTask(PollingConfiguration(), log);

            task.Start();

            log.Received().Info(Arg.Is<string>(s => s.StartsWith("Pipelines runner SQ-TEST started, polling https://127.0.0.1:")));

            var stopwatch = Stopwatch.StartNew();
            task.Stop();
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task LogsAnErrorWhenTheGatewayLoopFaults()
        {
            var log = Substitute.For<ISystemLog>();
            var loggedFault = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            log.When(l => l.Error(Arg.Any<Exception>(), "Pipelines runner stopped unexpectedly")).Do(_ => loggedFault.TrySetResult(true));
            // A wss:// address makes GrpcChannel.ForAddress throw inside the gateway loop, after Start has returned.
            var task = new RunnerBackgroundTask(PollingConfiguration(), log, () => false, () => false,
                configureGateway: o => o.ServerAddress = "wss://127.0.0.1:1");

            task.Start();
            try
            {
                (await Task.WhenAny(loggedFault.Task, Task.Delay(TimeSpan.FromSeconds(10)))).Should().Be(loggedFault.Task, "the faulted gateway loop should be logged");
            }
            finally
            {
                task.Stop();
            }
        }

        [Test]
        public async Task StopReturnsWithinTheTimeoutWhileAJobIsStillRunning()
        {
            var log = Substitute.For<ISystemLog>();
            var backend = new HangingBackend();
            var task = NewTask(PollingConfiguration(), log, TimeSpan.FromSeconds(2), backend, new InMemoryLogStore());

            task.Start();
            task.Executor.Should().NotBeNull();
            task.Executor!.Start(new ExecuteJobCommand
            {
                JobId = "job-1",
                RunId = "run-1",
                Command = "sleep 1000",
                Storage = new JobStorage { DirectoryUri = "https://example.invalid/c-customer/runs/run-1/job-1" }
            });
            (await Task.WhenAny(backend.Running, Task.Delay(TimeSpan.FromSeconds(10)))).Should().Be(backend.Running, "the job should reach the backend");

            var stopwatch = Stopwatch.StartNew();
            task.Stop();
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4));
        }

        [Test]
        public async Task RunsJobsInDockerContainersByDefault()
        {
            if (!DockerAvailable())
                Assert.Ignore("Needs the docker CLI and a running Docker daemon.");

            var logStore = new InMemoryLogStore();
            var task = NewTask(PollingConfiguration(), Substitute.For<ISystemLog>(), logStore: logStore);

            task.Start();
            try
            {
                task.Executor.Should().NotBeNull();
                task.Executor!.Start(new ExecuteJobCommand
                {
                    JobId = "job-docker",
                    RunId = "run-1",
                    // /.dockerenv exists only inside a Docker container.
                    Command = "echo from-the-job; [ -f /.dockerenv ] && echo inside-a-container",
                    Storage = new JobStorage { DirectoryUri = "https://example.invalid/c-customer/runs/run-1/job-docker" }
                });
                var completed = task.Executor.WhenAllCompleted();
                (await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(120)))).Should().Be(completed, "the job should finish (the first run may pull the image)");

                logStore.Text.Should().Contain("from-the-job").And.Contain("inside-a-container");
            }
            finally
            {
                task.Stop();
            }
        }

        [Test]
        public void ModuleRegistersTheTaskAsABackgroundTask()
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(Substitute.For<ITentacleConfiguration>()).As<ITentacleConfiguration>();
            builder.RegisterInstance(Substitute.For<ISystemLog>()).As<ISystemLog>();
            builder.RegisterModule(new PipelinesRunnerModule());
            using var container = builder.Build();

            var tasks = container.Resolve<IEnumerable<IBackgroundTask>>().ToArray();

            tasks.Should().ContainSingle(t => t is RunnerBackgroundTask);
        }

        sealed class HangingBackend : IJobBackend
        {
            readonly TaskCompletionSource<bool> running = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Running => running.Task;

            public Task<int> RunAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
            {
                running.TrySetResult(true);
                // Ignores cancellation on purpose: Stop() must not wait on it forever.
                return new TaskCompletionSource<int>().Task;
            }
        }

        sealed class InMemoryLogStore : ILogStore
        {
            readonly MemoryStream written = new();

            /// <summary>Everything appended to any log, as UTF-8 text.</summary>
            public string Text
            {
                get
                {
                    lock (written)
                    {
                        return Encoding.UTF8.GetString(written.ToArray());
                    }
                }
            }

            public Task<ILogAppender> CreateAsync(Uri jobDirectory, string fileName, CancellationToken ct)
                => Task.FromResult<ILogAppender>(new Appender(written));

            sealed class Appender : ILogAppender
            {
                readonly MemoryStream written;

                public Appender(MemoryStream written) => this.written = written;

                public long Length { get; private set; }

                public Task AppendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
                {
                    lock (written)
                    {
                        written.Write(data.Span);
                    }

                    Length += data.Length;
                    return Task.CompletedTask;
                }

                public ValueTask DisposeAsync() => default;
            }
        }

        static bool DockerAvailable()
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("docker", "version --format {{.Server.Version}}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                })!;
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }
    }
}
#endif
