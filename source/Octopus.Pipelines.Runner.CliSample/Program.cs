using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octopus.Pipelines.Runner.CliSample;
using Octopus.Pipelines.Runner.Contracts;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Pipelines.Runner.Core.Steps;
using Octopus.Pipelines.Runner.Docker;
using Octopus.Pipelines.Runner.Gateway;
using Octopus.Pipelines.Runner.Workflows;

// Runs one command, or one job of a workflow file, through the same gateway as Tentacle and the Kubernetes runner: the
// CLI plays the Server (CliJobSource) and the runner's GatewayClient reaches it in process, with no network.

const string usage = """
    usage: Octopus.Pipelines.Runner.CliSample [--image <image>] [--log-root <dir>] -- <command>
           Octopus.Pipelines.Runner.CliSample [--image <image>] [--log-root <dir>] --workflow <file> [--job <id>]
               [--secret <name>=<value>]... [--step-runner <path>] [--keep-state]
    """;
const int CancelledExitCode = 130;

string? image = null;
var logRoot = ".runner-logs";
string? command = null;
string? workflowPath = null;
string? jobName = null;
var secrets = new Dictionary<string, string>();
var stepRunner = Environment.GetEnvironmentVariable("OCTOPUS_STEP_RUNNER") ?? Path.Combine(AppContext.BaseDirectory, "octopus-step-runner");
var keepState = false;

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
        case "--workflow" when i + 1 < args.Length:
            workflowPath = args[++i];
            break;
        case "--job" when i + 1 < args.Length:
            jobName = args[++i];
            break;
        case "--secret" when i + 1 < args.Length && args[i + 1].IndexOf('=') > 0:
            var secret = args[++i];
            secrets[secret[..secret.IndexOf('=')]] = secret[(secret.IndexOf('=') + 1)..];
            break;
        case "--step-runner" when i + 1 < args.Length:
            stepRunner = args[++i];
            break;
        case "--keep-state":
            keepState = true;
            break;
        default:
            return Usage();
    }
}

if (string.IsNullOrWhiteSpace(command) == (workflowPath is null))
{
    return Usage();
}

WorkflowJob? workflow = null;
if (workflowPath is not null)
{
    try
    {
        workflow = WorkflowFile.Load(workflowPath, jobName);
    }
    catch (Exception e) when (e is FormatException or IOException)
    {
        Console.Error.WriteLine($"error: {e.Message}");
        return 2;
    }
}

var root = Path.GetFullPath(logRoot);
var runId = Guid.NewGuid().ToString("N");

// Warnings and errors only, on stderr: stdout carries the job's output. A job's own failure is printed below as
// "error: ...", so JobRunner's warning about it would only repeat it.
using var loggerFactory = new StderrLoggerFactory(
    LogLevel.Warning,
    new Dictionary<string, LogLevel> { [typeof(JobRunner).FullName!] = LogLevel.Error });

var clock = TimeProvider.System;
var dockerOptions = new DockerBackendOptions
{
    StepRunnerPath = stepRunner,
    StateRoot = Path.Combine(root, "state"),
    KeepState = keepState,
};
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

var source = new CliJobSource(heartbeatInterval: TimeSpan.FromSeconds(30));
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

var stopwatch = Stopwatch.StartNew();
await client.StartAsync(CancellationToken.None);
using var stdout = Console.OpenStandardOutput();

var job = new ExecuteJobCommand
{
    JobId = Guid.NewGuid().ToString("N"),
    RunId = runId,
    Isolation = Octopus.Pipelines.Runner.Contracts.JobIsolation.Standard,
};
if (workflow is null)
{
    job.Command = command;
}
else
{
    job.Steps.AddRange(workflow.Steps.Select(ToProto));
    job.Env.Add(workflow.Env.ToDictionary());
    job.Secrets.Add(secrets);
    job.Services.AddRange(workflow.Services.Select(s => new Service { Name = s.Name, Image = s.Image, Env = { s.Env.ToDictionary() } }));
}

var jobDirectory = Path.Combine(root, "local", "runs", runId, job.JobId);
job.Storage = new JobStorage { DirectoryUri = new Uri(jobDirectory).AbsoluteUri };
var finishedTask = source.RunAsync(job);
await LogTail.CopyAsync(Path.Combine(jobDirectory, JobFiles.Log), stdout, finishedTask);
var finished = await finishedTask;

await client.StopAsync(CancellationToken.None);

if (finished.Error.Length > 0)
{
    Console.Error.WriteLine($"error: {finished.Error}");
}

if (workflow is not null)
{
    Console.Error.WriteLine($"elapsed={stopwatch.Elapsed.TotalMilliseconds:F0}ms");
    foreach (var step in finished.Steps)
    {
        var outputs = string.Join(' ', step.Outputs.Select(o => $"{o.Key}={o.Value.Replace("\n", "\\n")}"));
        var error = step.Error.Length > 0 ? $" error=\"{step.Error}\"" : "";
        Console.Error.WriteLine($"  {step.Id,-8} {step.Outcome.ToString().ToLowerInvariant(),-8} exit={(step.HasExitCode ? step.ExitCode : "-"),-3} {outputs}{error}");
    }
}

Console.Error.WriteLine($"exit={finished.ExitCode} log={Path.Combine(jobDirectory, JobFiles.Log)}");
return lifetime.ApplicationStopping.IsCancellationRequested ? CancelledExitCode
    : finished.Error.Length > 0 ? 1
    : finished.ExitCode;

static Step ToProto(StepSpec step)
{
    var proto = new Step
    {
        Id = step.Id,
        Name = step.Name ?? "",
        Run = step.Run,
        Condition = step.If ?? "",
        Image = step.Image ?? "",
        Shell = step.Shell ?? "",
        TimeoutSeconds = step.Timeout is { } timeout ? (int)Math.Ceiling(timeout.TotalSeconds) : 0,
    };
    proto.Env.Add(step.Env.ToDictionary());
    return proto;
}

static int Usage()
{
    Console.Error.WriteLine(usage);
    return 2;
}
