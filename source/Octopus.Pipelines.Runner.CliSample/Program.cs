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
using StepResult = Octopus.Pipelines.Runner.Contracts.StepResult;
using StepOutcome = Octopus.Pipelines.Runner.Contracts.StepOutcome;

// Runs one command, or one job of a workflow file, through the same gateway as Tentacle and the Kubernetes runner: the
// CLI plays the Server (CliJobSource) and the runner's GatewayClient reaches it in process, with no network.

const string usage = """
    usage: Octopus.Pipelines.Runner.CliSample [--image <image>] [--log-root <dir>] -- <command>
           Octopus.Pipelines.Runner.CliSample [--image <image>] [--log-root <dir>] --workflow <file> [--job <id>]
               [--strategy server|run|exec|in-container] [--keep-state]
    """;
const int CancelledExitCode = 130;

string? image = null;
var logRoot = ".runner-logs";
string? command = null;
string? workflowPath = null;
string? jobName = null;
var strategy = "run";
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
        case "--strategy" when i + 1 < args.Length && args[i + 1] is "server" or "run" or "exec" or "in-container":
            strategy = args[++i];
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
    catch (Exception e) when (e is FormatException or IOException or YamlDotNet.Core.YamlException)
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
    // The runner cell's choice of how to run a job's steps. With the server strategy every job has one step.
    StepMode = strategy switch
    {
        "exec" => DockerStepMode.Exec,
        "in-container" => DockerStepMode.InContainer,
        _ => DockerStepMode.Run,
    },
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

List<MarkJobAsFinishedCommand> jobs = [];
if (workflow is null)
{
    jobs.Add(await RunJobAsync(new ExecuteJobCommand { Command = command }));
}
else if (strategy == "server")
{
    await OrchestrateOnServerAsync(workflow);
}
else
{
    // The runner gets the whole job, expressions and all, and orchestrates its steps as its backend sees fit.
    var job = new ExecuteJobCommand();
    job.Steps.AddRange(workflow.Steps.Select(ToProto));
    jobs.Add(await RunJobAsync(job));
}

await client.StopAsync(CancellationToken.None);
var elapsed = stopwatch.Elapsed;

var error = jobs.Select(j => j.Error).FirstOrDefault(e => e.Length > 0);
if (error is not null)
{
    Console.Error.WriteLine($"error: {error}");
}

var steps = jobs.SelectMany(j => j.Steps).ToArray();
if (workflow is not null)
{
    Console.Error.WriteLine($"strategy={strategy} jobs={jobs.Count} elapsed={elapsed.TotalMilliseconds:F0}ms");
    foreach (var step in steps)
    {
        var outputs = string.Join(' ', step.Outputs.Select(o => $"{o.Key}={o.Value.Replace("\n", "\\n")}"));
        Console.Error.WriteLine($"  {step.Id,-8} {step.Outcome.ToString().ToLowerInvariant(),-8} exit={(step.HasExitCode ? step.ExitCode : "-"),-3} {outputs}");
    }
}

var exitCode = jobs.Select(j => j.ExitCode).FirstOrDefault(c => c != 0);
Console.Error.WriteLine($"exit={exitCode} logs={Path.Combine(root, "local", "runs", runId)}");
return lifetime.ApplicationStopping.IsCancellationRequested ? CancelledExitCode
    : error is not null ? 1
    : exitCode;

// The Server orchestrates: it evaluates each step's condition and expressions against the outputs earlier steps
// reported, and sends each step that should run to a runner as its own single-step job. The runner sees no expressions.
async Task OrchestrateOnServerAsync(WorkflowJob job)
{
    var context = new StepsContext();
    foreach (var step in job.Steps)
    {
        if (lifetime.ApplicationStopping.IsCancellationRequested)
        {
            return;
        }

        if (!ExprEvaluator.IsTrue(Expr.ParseCondition(step.If), context))
        {
            WriteOut($"==> {step.Id}: {step.DisplayName} skipped (by the server)\n");
            var skipped = Octopus.Pipelines.Runner.Core.Steps.StepResult.Skipped(step.Id);
            context.Add(skipped);
            jobs.Add(new MarkJobAsFinishedCommand { Steps = { new StepResult { Id = step.Id, Outcome = StepOutcome.Skipped } } });
            continue;
        }

        var resolved = step with
        {
            Run = ExprEvaluator.Interpolate(step.Run, context),
            If = null,
            Env = step.Env.ToDictionary(e => e.Key, e => ExprEvaluator.Interpolate(e.Value, context)),
        };
        var assignment = new ExecuteJobCommand();
        assignment.Steps.Add(ToProto(resolved));
        var finished = await RunJobAsync(assignment);
        jobs.Add(finished);

        var result = finished.Steps.SingleOrDefault();
        context.Add(new Octopus.Pipelines.Runner.Core.Steps.StepResult(
            step.Id,
            result?.Outcome == StepOutcome.Success ? Octopus.Pipelines.Runner.Core.Steps.StepOutcome.Success : Octopus.Pipelines.Runner.Core.Steps.StepOutcome.Failure,
            result?.ExitCode,
            result?.Outputs.ToDictionary() ?? []));
    }
}

async Task<MarkJobAsFinishedCommand> RunJobAsync(ExecuteJobCommand job)
{
    job.JobId = Guid.NewGuid().ToString("N");
    job.RunId = runId;
    job.Isolation = Octopus.Pipelines.Runner.Contracts.JobIsolation.Standard;
    var jobDirectory = Path.Combine(root, "local", "runs", runId, job.JobId);
    job.Storage = new JobStorage { DirectoryUri = new Uri(jobDirectory).AbsoluteUri };

    var finished = source.RunAsync(job);
    await LogTail.CopyAsync(Path.Combine(jobDirectory, JobFiles.Log), stdout, finished);
    return await finished;
}

void WriteOut(string text)
{
    stdout.Write(System.Text.Encoding.UTF8.GetBytes(text));
    stdout.Flush();
}

static Step ToProto(StepSpec step)
{
    var proto = new Step
    {
        Id = step.Id,
        Name = step.Name ?? "",
        Run = step.Run,
        Condition = step.If ?? "",
        Image = step.Image ?? "",
    };
    foreach (var (name, value) in step.Env)
    {
        proto.Env[name] = value;
    }

    return proto;
}

static int Usage()
{
    Console.Error.WriteLine(usage);
    return 2;
}
