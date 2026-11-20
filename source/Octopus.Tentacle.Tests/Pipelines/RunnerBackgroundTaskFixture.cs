#if PIPELINES_RUNNER
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
        static X509Certificate2 NewCertificate()
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        }

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
            configuration.TentacleCertificate.Returns(NewCertificate());
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
            configuration.TentacleCertificate.Returns(NewCertificate());
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
            public Task<ILogAppender> CreateAsync(Uri jobDirectory, string fileName, CancellationToken ct)
                => Task.FromResult<ILogAppender>(new Appender());

            sealed class Appender : ILogAppender
            {
                public long Length { get; private set; }

                public Task AppendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
                {
                    Length += data.Length;
                    return Task.CompletedTask;
                }

                public ValueTask DisposeAsync() => default;
            }
        }
    }
}
#endif
