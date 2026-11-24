using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Pipelines.Runner.Gateway;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class GatewayClientTests
{
    [Fact]
    public async Task AddRunnerGateway_gives_the_client_a_channel_to_the_configured_server()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, StubLifetime>();
        services.AddSingleton<IJobBackend>(new ProcessJobBackend(TimeProvider.System));
        services.AddSingleton<ILogStore>(new FileLogStore(Path.GetTempPath()));
        var section = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ServerAddress"] = "http://127.0.0.1:1" })
            .Build();
        services.AddRunnerGateway(section);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.Single(provider.GetServices<IHostedService>().OfType<GatewayClient>());
        Assert.Equal("127.0.0.1:1", provider.GetRequiredService<Grpc.Net.Client.GrpcChannel>().Target);
    }

    [Fact]
    public async Task Repeated_connection_failures_warn_once()
    {
        var port = GetClosedPort();
        var logger = new CapturingLogger<GatewayClient>();
        var outbox = new Outbox();
        var signal = new CapacitySignal();
        var root = Path.Combine(Path.GetTempPath(), "tspoc-gateway-client-tests", Guid.NewGuid().ToString("N"));
        var executor = new GatewayJobExecutor(
            new JobRunner(new ProcessJobBackend(TimeProvider.System), new FileLogStore(root), new JobRunnerOptions()),
            outbox,
            signal,
            TimeProvider.System,
            new StubLifetime(),
            NullLogger<GatewayJobExecutor>.Instance);
        var options = Options.Create(new GatewayOptions { ServerAddress = $"http://127.0.0.1:{port}" });
        using var channel = GatewayChannel.Create(options.Value);
        var client = new GatewayClient(executor, outbox, new FixedCapacityProvider(executor, options), signal, options, logger, channel.CreateCallInvoker())
        {
            InitialBackoff = TimeSpan.FromMilliseconds(10),
            MaxJitter = TimeSpan.Zero,
        };

        await client.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (logger.Entries.Count(e => e.Message.Contains("failed")) < 3)
            {
                await Task.Delay(20, timeout.Token);
            }
        }
        finally
        {
            await client.StopAsync(CancellationToken.None);
        }

        var failures = logger.Entries.Where(e => e.Message.Contains("failed")).ToList();
        Assert.True(failures.Count >= 3);
        Assert.Single(failures, e => e.Level == LogLevel.Warning);
        Assert.Equal(LogLevel.Warning, failures[0].Level);
        Assert.All(failures.Skip(1), e => Assert.Equal(LogLevel.Debug, e.Level));
    }

    [Fact]
    public async Task Reconnect_backoff_doubles_up_to_MaxReconnectBackoff()
    {
        var port = GetClosedPort();
        var outbox = new Outbox();
        var signal = new CapacitySignal();
        var root = Path.Combine(Path.GetTempPath(), "tspoc-gateway-client-tests", Guid.NewGuid().ToString("N"));
        var executor = new GatewayJobExecutor(
            new JobRunner(new ProcessJobBackend(TimeProvider.System), new FileLogStore(root), new JobRunnerOptions()),
            outbox,
            signal,
            TimeProvider.System,
            new StubLifetime(),
            NullLogger<GatewayJobExecutor>.Instance);
        var options = Options.Create(new GatewayOptions
        {
            ServerAddress = $"http://127.0.0.1:{port}",
            MaxReconnectBackoff = TimeSpan.FromMilliseconds(40),
        });
        var delays = new List<TimeSpan>();
        var enoughDelays = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var channel = GatewayChannel.Create(options.Value);
        var client = new GatewayClient(executor, outbox, new FixedCapacityProvider(executor, options), signal, options, NullLogger<GatewayClient>.Instance, channel.CreateCallInvoker())
        {
            InitialBackoff = TimeSpan.FromMilliseconds(10),
            MaxJitter = TimeSpan.Zero,
            // Records what the loop asks for and waits only a moment, so the sequence is deterministic and quick.
            Delay = (delay, ct) =>
            {
                lock (delays)
                {
                    delays.Add(delay);
                    if (delays.Count >= 6)
                    {
                        enoughDelays.TrySetResult();
                    }
                }

                return Task.Delay(TimeSpan.FromMilliseconds(1), ct);
            },
        };

        await client.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await enoughDelays.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
        finally
        {
            await client.StopAsync(CancellationToken.None);
        }

        TimeSpan[] recorded;
        lock (delays)
        {
            recorded = [.. delays.Take(6)];
        }

        Assert.Equal(
            new[] { 10, 20, 40, 40, 40, 40 }.Select(ms => TimeSpan.FromMilliseconds(ms)),
            recorded);
    }

    [Fact]
    public void MaxReconnectBackoff_defaults_to_30_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), new GatewayOptions().MaxReconnectBackoff);
    }

    static int GetClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    sealed class StubLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }

    sealed class CapturingLogger<T> : ILogger<T>
    {
        readonly object gate = new();
        readonly List<(LogLevel Level, string Message)> entries = [];

        public List<(LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (gate)
                {
                    return [.. entries];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (gate)
            {
                entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
