using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octopus.Pipelines.Runner.CliSample;
using Octopus.Pipelines.Runner.Contracts;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Pipelines.Runner.Docker;
using Octopus.Pipelines.Runner.Gateway;

// Runs one command as a pipeline job, through the same gateway as Tentacle and the Kubernetes runner: the CLI plays
// the Server (CliJobSource) and the runner's GatewayClient reaches it in process, with no network.

const string usage = "usage: Octopus.Pipelines.Runner.CliSample [--image <image>] [--log-root <dir>] -- <command>";
const int CancelledExitCode = 130;

string? image = null;
var logRoot = ".runner-logs";
string? command = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--":
            command = string.Join(' ', args.Skip(i + 1));
            i = args.Length;
            break;
        case "--image" when i + 1 < args.Length:
            image = args[++i];
            break;
        case "--log-root" when i + 1 < args.Length:
            logRoot = args[++i];
            break;
        default:
            return Usage();
    }
}

if (string.IsNullOrWhiteSpace(command))
{
    return Usage();
}

var root = Path.GetFullPath(logRoot);
var runId = Guid.NewGuid().ToString("N");
var jobId = Guid.NewGuid().ToString("N");
var jobDirectoryPath = Path.Combine(root, "local", "runs", runId, jobId);
var logPath = Path.Combine(jobDirectoryPath, JobFiles.Log);

// Warnings and errors only, on stderr: stdout carries the job's output. A job's own failure is printed below as
// "error: ...", so JobRunner's warning about it would only repeat it.
using var loggerFactory = new StderrLoggerFactory(
    LogLevel.Warning,
    new Dictionary<string, LogLevel> { [typeof(JobRunner).FullName!] = LogLevel.Error });

var clock = TimeProvider.System;
var dockerOptions = new DockerBackendOptions();
if (image is not null)
{
    dockerOptions.JobImage = image;
}

using var backend = new DockerJobBackend(dockerOptions, clock, loggerFactory.CreateLogger<DockerJobBackend>());
var jobRunner = new JobRunner(
    backend,
    new LogStores(new Dictionary<string, ILogStore> { ["file"] = new FileLogStore(root) }),
    // Flush often: the terminal echo tails the log file.
    new JobRunnerOptions { LogFlushInterval = TimeSpan.FromMilliseconds(100) },
    clock,
    loggerFactory.CreateLogger<JobRunner>());

var source = new CliJobSource(
    new ExecuteJobCommand
    {
        JobId = jobId,
        RunId = runId,
        Command = command,
        Isolation = Octopus.Pipelines.Runner.Contracts.JobIsolation.Standard,
        Storage = new JobStorage { DirectoryUri = new Uri(jobDirectoryPath).AbsoluteUri },
    },
    heartbeatInterval: TimeSpan.FromSeconds(30));

var outbox = new Outbox();
var capacitySignal = new CapacitySignal();
var lifetime = new RunnerLifetime();
var executor = new GatewayJobExecutor(jobRunner, outbox, capacitySignal, clock, lifetime, loggerFactory.CreateLogger<GatewayJobExecutor>());
var options = Options.Create(new GatewayOptions { ServerAddress = "in-process", CellId = "local", RunnerId = "cli", MaxConcurrentJobs = 1 });
using var client = new GatewayClient(
    executor,
    outbox,
    new FixedCapacityProvider(executor, options),
    capacitySignal,
    options,
    loggerFactory.CreateLogger<GatewayClient>(),
    new InProcessCallInvoker(binder => RunnerGateway.BindService(binder, source)));

// Ctrl+C cancels the job the way stopping Tentacle does; the runner still reports it finished, so wait for that.
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    lifetime.StopApplication();
};

await client.StartAsync(CancellationToken.None);
using var stdout = Console.OpenStandardOutput();
var echo = LogTail.CopyAsync(logPath, stdout, source.Finished.Task);
var finished = await source.Finished.Task;
await echo;
await client.StopAsync(CancellationToken.None);

if (finished.Error.Length > 0)
{
    Console.Error.WriteLine($"error: {finished.Error}");
}

Console.Error.WriteLine($"exit={finished.ExitCode} log={logPath}");
return lifetime.ApplicationStopping.IsCancellationRequested ? CancelledExitCode
    : finished.Error.Length > 0 ? 1
    : finished.ExitCode;

static int Usage()
{
    Console.Error.WriteLine(usage);
    return 2;
}
