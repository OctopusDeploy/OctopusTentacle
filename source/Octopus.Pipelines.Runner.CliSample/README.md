# Octopus.Pipelines.Runner.CliSample

Reference for embedding the runner core in a Native AOT .NET CLI that runs jobs in Docker on the same
machine. It references only `Octopus.Pipelines.Runner.Core` and `Octopus.Pipelines.Runner.Docker` and uses no Generic Host.

```
Octopus.Pipelines.Runner.CliSample [--image <image>] [--log-root <dir>] -- <command>
Octopus.Pipelines.Runner.CliSample [--image <image>] [--log-root <dir>] --workflow <file> [--job <id>]
    [--secret <name>=<value>]... [--step-runner <path>] [--keep-state]
```

Everything after `--` is joined with spaces into the job command. The job's output goes to stdout, the log to
`<log-root>/local/runs/<run>/<job>/job.log`, and `exit=<code> log=<path>` to stderr. The exit code is the job's
(1 if the run failed with an error, 2 for bad usage). Ctrl+C cancels the run and removes the container.

```csharp
using var backend = new DockerJobBackend(new DockerBackendOptions { JobImage = "busybox:1.37" });
var logs = new LogStores(new Dictionary<string, ILogStore> { ["file"] = new FileLogStore(root) });
var runner = new JobRunner(backend, logs, new JobRunnerOptions());

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var jobDirectory = new Uri(Path.Combine(root, "local", "runs", runId, jobId));
var result = await runner.RunAsync(new JobSpec(jobId, runId, command), jobDirectory,
    new StdoutObserver(Console.OpenStandardOutput()), cts.Token);
return result.Error is not null ? 1 : result.ExitCode;

sealed class StdoutObserver(Stream stdout) : IJobObserver
{
    public void Output(ReadOnlySpan<byte> data) { stdout.Write(data); stdout.Flush(); }
}
```

## Workflows

`--workflow` runs one job of a GitHub Actions workflow file: its `container`, `env`, `services` and `run` steps with
`id`, `name`, `if`, `env`, `shell` and `timeout-minutes`. A step may also set its own `container`, which GitHub
Actions doesn't allow on a `run` step; it overrides the job's. `workflows/` has three samples: every step in the
job's container, a different container for the middle step, and a different container for every step.

Steps run one at a time, each in its own `docker run --rm`, with the step runner (`Octopus.Pipelines.Runner.StepRunner`)
bind mounted as the container's entry point. The step runner reads `$OCTOPUS_INPUT` (the step and the context so
far), decides from the step's `if` whether it runs, resolves its `${{ }}` expressions, runs the script with bash if
the image has it (else sh) in `$OCTOPUS_WORKSPACE`, masks secrets in its output, and writes a result with the
step's outcome and the outputs it wrote to `$OCTOPUS_OUTPUT` (also `$GITHUB_OUTPUT`). The job's directory, at
`<log-root>/state`, is the shared storage: the Docker daemon must be local. `--keep-state` leaves it behind.

The step runner must be the statically linked build: `./build.sh --target PublishPipelinesStepRunner` writes it to
`_artifacts/pipelines-step-runner/<rid>/octopus-step-runner`. Pass it with `--step-runner` or `OCTOPUS_STEP_RUNNER`;
by default the CLI looks next to itself.

```
Octopus.Pipelines.Runner.CliSample --workflow workflows/3-different-containers.yml \
    --step-runner _artifacts/pipelines-step-runner/linux-arm64/octopus-step-runner --secret DEPLOY_TOKEN=abc
```

The summary on stderr lists each step's outcome, exit code and outputs; outputs that contain a secret stay inside
the job and aren't listed.

## AOT gate

`./build.sh --target CheckPipelinesRunnerAot` publishes with Native AOT, fails on any `warning IL` / `warning AOT`, checks
`-- echo hi` prints `hi`, and prints the median wall time of five `-- true` runs. It publishes for the host's
RID (`linux-x64` or `linux-arm64`), since Native AOT can't link for another architecture without a
cross toolchain.

## Start time

Median start-to-exit for `-- true`: **74 ms** (runs: 70 78 74 67 88 ms), binary 6.9 MB. Measured in the
development sandbox (linux-arm64, local Docker daemon) with the `busybox:1.37` image already pulled
(warm image cache).
