#if PIPELINES_RUNNER
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Pipelines.Runner.Docker;
using Octopus.Pipelines.Runner.Gateway;
using Octopus.Pipelines.Runner.Logs.Http;
using Octopus.Tentacle.Background;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Pipelines
{
    /// <summary>
    /// Runs the pipelines runner inside Tentacle: it polls the Tentacle's polling Server for jobs and runs each in a
    /// container on the local Docker daemon (Docker is assumed to be installed). It never stops Tentacle from
    /// starting: a misconfiguration or failure is logged and the runner stays off.
    /// </summary>
    public sealed class RunnerBackgroundTask : IBackgroundTask
    {
        static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(30);

        readonly ITentacleConfiguration configuration;
        readonly ISystemLog log;
        readonly Func<bool> isKubernetesAgent;
        readonly Func<bool> isWindows;
        readonly TimeSpan stopTimeout;
        readonly IJobBackend? backend;
        readonly ILogStore? logStore;
        readonly Action<GatewayOptions>? configureGateway;
        readonly object gate = new();

        Running? running;

        public RunnerBackgroundTask(ITentacleConfiguration configuration, ISystemLog log)
            : this(configuration, log, () => KubernetesSupportDetection.IsRunningAsKubernetesAgent, OperatingSystem.IsWindows)
        {
        }

        internal RunnerBackgroundTask(
            ITentacleConfiguration configuration,
            ISystemLog log,
            Func<bool> isKubernetesAgent,
            Func<bool> isWindows,
            TimeSpan? stopTimeout = null,
            IJobBackend? backend = null,
            ILogStore? logStore = null,
            Action<GatewayOptions>? configureGateway = null)
        {
            this.configuration = configuration;
            this.log = log;
            this.isKubernetesAgent = isKubernetesAgent;
            this.isWindows = isWindows;
            this.stopTimeout = stopTimeout ?? DefaultStopTimeout;
            this.backend = backend;
            this.logStore = logStore;
            this.configureGateway = configureGateway;
        }

        /// <summary>The running runner's job executor, so tests can hand it a job. Null when not running.</summary>
        internal GatewayJobExecutor? Executor
        {
            get
            {
                lock (gate)
                {
                    return running?.Executor;
                }
            }
        }

        public void Start()
        {
            lock (gate)
            {
                if (running is not null)
                    return;

                LoggerFactory? loggerFactory = null;
                DockerJobBackend? ownedBackend = null;
                HttpLogStore? ownedLogStore = null;
                try
                {
                    var settings = TentacleRunnerSettings.From(configuration, isKubernetesAgent(), isWindows(), out var skipReason);
                    if (settings is null)
                    {
                        log.Info(skipReason!);
                        return;
                    }

                    loggerFactory = new LoggerFactory(new[] { new SystemLogLoggerProvider(log) });

                    var clock = TimeProvider.System;
                    var gatewayOptions = new GatewayOptions
                    {
                        ServerAddress = settings.ServerAddress.ToString(),
                        CellId = TentacleRunnerSettings.CellId,
                        RunnerId = settings.RunnerId,
                        MaxConcurrentJobs = TentacleRunnerSettings.MaxConcurrentJobs,
                        MaxReconnectBackoff = TentacleRunnerSettings.MaxReconnectBackoff
                    };
                    configureGateway?.Invoke(gatewayOptions);
                    var options = Options.Create(gatewayOptions);

                    if (backend is null)
                        ownedBackend = CreateDefaultBackend(clock, loggerFactory);

                    var jobRunner = new JobRunner(
                        backend ?? ownedBackend!,
                        logStore ?? new LogStores(new Dictionary<string, ILogStore> { ["https"] = ownedLogStore = new HttpLogStore() }),
                        new JobRunnerOptions(),
                        clock,
                        loggerFactory.CreateLogger<JobRunner>());
                    var outbox = new Outbox();
                    var capacitySignal = new CapacitySignal();
                    var lifetime = new RunnerLifetime();
                    var executor = new GatewayJobExecutor(jobRunner, outbox, capacitySignal, clock, lifetime, loggerFactory.CreateLogger<GatewayJobExecutor>());
                    var client = new GatewayClient(
                        executor,
                        outbox,
                        new FixedCapacityProvider(executor, options),
                        capacitySignal,
                        options,
                        loggerFactory.CreateLogger<GatewayClient>());

                    client.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

                    // The gateway loop retries connection failures itself, so a fault means it has stopped for good.
                    client.ExecuteTask?.ContinueWith(
                        t => log.Error(t.Exception!.GetBaseException(), "Pipelines runner stopped unexpectedly"),
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted,
                        TaskScheduler.Default);

                    running = new Running(client, executor, lifetime, loggerFactory, ownedBackend, ownedLogStore);
                    log.Info($"Pipelines runner {settings.RunnerId} started, polling {settings.ServerAddress}");
                }
                catch (Exception ex)
                {
                    log.Error(ex, "The pipelines runner could not start. Tentacle will keep running without it.");
                    ownedBackend?.Dispose();
                    ownedLogStore?.Dispose();
                    loggerFactory?.Dispose();
                    running = null;
                }
            }
        }

        public void Stop()
        {
            lock (gate)
            {
                if (running is null)
                    return;

                var stopping = running;
                running = null;

                try
                {
                    var stopwatch = Stopwatch.StartNew();

                    // Cancels running jobs: GatewayJobExecutor runs them with ApplicationStopping.
                    stopping.Lifetime.StopApplication();

                    using (var timeout = new CancellationTokenSource(stopTimeout))
                    {
                        stopping.Client.StopAsync(timeout.Token).GetAwaiter().GetResult();
                    }

                    var remaining = stopTimeout - stopwatch.Elapsed;
                    var completed = remaining > TimeSpan.Zero && stopping.Executor.WhenAllCompleted().Wait(remaining);
                    if (!completed && stopping.Executor.InFlightCount > 0)
                        log.Warn($"The pipelines runner stopped with {stopping.Executor.InFlightCount} job(s) still running after {stopTimeout.TotalSeconds:0}s.");
                    else
                        log.Info("Pipelines runner stopped");
                }
                catch (Exception ex)
                {
                    log.Error(ex, "The pipelines runner did not stop cleanly.");
                }
                finally
                {
                    try
                    {
                        stopping.Client.Dispose();
                        stopping.OwnedBackend?.Dispose();
                        stopping.OwnedLogStore?.Dispose();
                        stopping.LoggerFactory.Dispose();
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex, "The pipelines runner did not stop cleanly.");
                    }
                }
            }
        }

        /// <summary>Jobs run in containers on the local Docker daemon, with the backend's default image and limits.</summary>
        internal static DockerJobBackend CreateDefaultBackend(TimeProvider clock, ILoggerFactory loggerFactory)
            => new DockerJobBackend(new DockerBackendOptions(), clock, logger: loggerFactory.CreateLogger<DockerJobBackend>());

        sealed class Running
        {
            public Running(GatewayClient client, GatewayJobExecutor executor, RunnerLifetime lifetime, LoggerFactory loggerFactory, DockerJobBackend? ownedBackend, HttpLogStore? ownedLogStore)
            {
                Client = client;
                Executor = executor;
                Lifetime = lifetime;
                LoggerFactory = loggerFactory;
                OwnedBackend = ownedBackend;
                OwnedLogStore = ownedLogStore;
            }

            public GatewayClient Client { get; }
            public GatewayJobExecutor Executor { get; }
            public RunnerLifetime Lifetime { get; }
            public LoggerFactory LoggerFactory { get; }

            /// <summary>The backend this task created (not one a test passed in), disposed when the runner stops.</summary>
            public DockerJobBackend? OwnedBackend { get; }

            /// <summary>The HTTPS log store this task created (not one a test passed in), disposed when the runner stops.</summary>
            public HttpLogStore? OwnedLogStore { get; }
        }
    }
}
#endif
