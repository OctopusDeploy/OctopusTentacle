using System.Threading.Channels;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Octopus.Pipelines.Runner.Contracts;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Pipelines.Runner.Gateway;

namespace Octopus.Pipelines.Runner.Tests;

/// <summary>The runner's real gateway talking to a Server in the same process, as the CLI does.</summary>
public sealed class InProcessGatewayTests
{
    [Fact]
    public async Task Runs_a_job_through_the_gateway_without_a_network()
    {
        var root = Path.Combine(Path.GetTempPath(), "pipelines-inprocess-tests", Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "local", "runs", "run-1", "job-1");
        var server = new OneJobServer(new ExecuteJobCommand
        {
            JobId = "job-1",
            RunId = "run-1",
            Command = "echo hi; exit 3",
            Storage = new JobStorage { DirectoryUri = new Uri(directory).AbsoluteUri },
        });
        using var gateway = Gateway(server, new FileLogStore(root), NullLogger<GatewayClient>.Instance);

        await gateway.Client.StartAsync(TestContext.Current.CancellationToken);
        var finished = await server.Finished.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await gateway.Client.StopAsync(TestContext.Current.CancellationToken);

        // After the job the runner asks for more work, which can race MarkJobAsFinished: only this much is ordered.
        Assert.Equal(["Hello", "AllocateNextJob", "JobStarted"], server.Received.Take(3));
        Assert.Equal("job-1", finished.JobId);
        Assert.Equal(JobOutcome.Failed, finished.Outcome);
        Assert.Equal(3, finished.ExitCode);
        Assert.Equal(3, finished.LogBytes);
        Assert.Equal("hi\n", await File.ReadAllTextAsync(Path.Combine(directory, JobFiles.Log), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Stopping_the_client_is_not_logged_as_a_connection_failure()
    {
        var server = new OneJobServer(job: null);
        var logger = new RecordingLogger<GatewayClient>();
        using var gateway = Gateway(server, new FileLogStore(Path.GetTempPath()), logger);

        await gateway.Client.StartAsync(TestContext.Current.CancellationToken);
        await server.Connected.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await gateway.Client.StopAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Calls_to_an_unbound_service_are_unimplemented()
    {
        var invoker = new InProcessCallInvoker(_ => { });
        var client = new RunnerGateway.RunnerGatewayClient(invoker);

        var error = Assert.Throws<RpcException>(() => client.Connect(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(StatusCode.Unimplemented, error.StatusCode);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Each_side_receives_its_own_copy_of_a_message()
    {
        var received = Channel.CreateUnbounded<RunnerMessage>();
        var service = new EchoService(received.Writer);
        var client = new RunnerGateway.RunnerGatewayClient(new InProcessCallInvoker(binder => RunnerGateway.BindService(binder, service)));
        using var call = client.Connect(cancellationToken: TestContext.Current.CancellationToken);
        var sent = new RunnerMessage { Hello = new Hello { CellId = "before" } };

        await call.RequestStream.WriteAsync(sent, TestContext.Current.CancellationToken);
        sent.Hello.CellId = "after";
        var copy = await received.Reader.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("before", copy.Hello.CellId);
        Assert.NotSame(sent, copy);
    }

    static TestGateway Gateway(RunnerGateway.RunnerGatewayBase server, ILogStore logs, ILogger<GatewayClient> logger)
    {
        var outbox = new Outbox();
        var signal = new CapacitySignal();
        var executor = new GatewayJobExecutor(
            new JobRunner(new ShellJobBackend(TimeProvider.System), logs, new JobRunnerOptions()),
            outbox,
            signal,
            TimeProvider.System,
            new RunnerLifetime(),
            NullLogger<GatewayJobExecutor>.Instance);
        var options = Options.Create(new GatewayOptions { ServerAddress = "in-process", MaxConcurrentJobs = 1 });
        var client = new GatewayClient(
            executor,
            outbox,
            new FixedCapacityProvider(executor, options),
            signal,
            options,
            logger,
            new InProcessCallInvoker(binder => RunnerGateway.BindService(binder, server)));
        return new TestGateway(client);
    }

    sealed record TestGateway(GatewayClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    /// <summary>A Server that hands out at most one job and records what the runner sends.</summary>
    sealed class OneJobServer(ExecuteJobCommand? job) : RunnerGateway.RunnerGatewayBase
    {
        int handedOut;

        public List<string> Received { get; } = [];

        public TaskCompletionSource Connected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<MarkJobAsFinishedCommand> Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task Connect(IAsyncStreamReader<RunnerMessage> requestStream, IServerStreamWriter<ServerMessage> responseStream, ServerCallContext context)
        {
            await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
            {
                lock (Received)
                {
                    Received.Add(message.BodyCase.ToString());
                }

                switch (message.BodyCase)
                {
                    case RunnerMessage.BodyOneofCase.Hello:
                        await responseStream.WriteAsync(new ServerMessage { Welcome = new Welcome { ServerId = "test" } });
                        Connected.TrySetResult();
                        break;
                    case RunnerMessage.BodyOneofCase.AllocateNextJob when job is not null && Interlocked.Exchange(ref handedOut, 1) == 0:
                        await responseStream.WriteAsync(new ServerMessage { ExecuteJob = job });
                        break;
                    case RunnerMessage.BodyOneofCase.MarkJobAsFinished:
                        Finished.TrySetResult(message.MarkJobAsFinished);
                        break;
                }
            }
        }
    }

    sealed class EchoService(ChannelWriter<RunnerMessage> received) : RunnerGateway.RunnerGatewayBase
    {
        public override async Task Connect(IAsyncStreamReader<RunnerMessage> requestStream, IServerStreamWriter<ServerMessage> responseStream, ServerCallContext context)
        {
            await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
            {
                await received.WriteAsync(message, context.CancellationToken);
            }
        }
    }

    sealed class RecordingLogger<T> : ILogger<T>
    {
        readonly List<(LogLevel Level, string Message)> entries = [];

        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (entries)
                {
                    return [.. entries];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (entries)
            {
                entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
