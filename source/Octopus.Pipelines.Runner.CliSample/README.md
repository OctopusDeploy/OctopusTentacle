# Octopus.Pipelines.Runner.CliSample

Reference for embedding the runner core in a Native AOT .NET CLI that runs jobs in Docker on the same
machine. It references only `Octopus.Pipelines.Runner.Core` and `Octopus.Pipelines.Runner.Docker` and uses no Generic Host.

```
Octopus.Pipelines.Runner.CliSample [--image <image>] [--log-root <dir>] -- <command>
Octopus.Pipelines.Runner.CliSample [--image <image>] [--log-root <dir>] --workflow <file> [--job <id>]
    [--secret <name>=<value>]... [--step-runner-image <image>] [--keep-state]
```

Everything after `--` is joined with spaces into the job command. The job's output goes to stdout, the log to
`<log-root>/local/runs/<run>/<job>/job.log`, and `exit=<code> log=<path>` to stderr. The exit code is the job's
(1 if the run errored, 2 for bad usage, 130 if it was cancelled). Ctrl+C cancels the job, as a user cancelling it
in the Server would: the running step stops, and steps that run after a cancellation (`if: always()`) still run. A
second Ctrl+C forces it to stop at once.

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

`--workflow` runs one job of a GitHub Actions workflow file: its `container` (with `credentials`), `env`,
`timeout-minutes`, `services` (with `env` and `options: --health-cmd`) and `run` steps with `id`, `name`, `if`, `env`,
`shell` and `timeout-minutes`. A step may also set its own `container`, which GitHub Actions doesn't allow on a `run`
step. `workflows/` has three samples: every step in the job's container, a different container for the middle step, and
a different container for every step.

The CLI plays the Server: it turns each step into a payload (its name, condition, script, env and the job's secrets)
and sends the job through the gateway. The runner never reads a payload. It:

- pulls the job's images, and extracts each step's step runner from its step runner image (`--step-runner-image`);
- starts one container per image, with the step runner idling as its main process, and runs each step in it with
  `docker exec`, so steps that share an image share a container, including what they install outside the workspace;
- writes each step's payload to `OCTOPUS_INPUT`, and appends each step's result to a results file the later steps read
  (mounted read-only);
- removes a container after the last step that needs it, and stops a step that times out or is cancelled.

The step runner decides from the payload whether the step runs, resolves its `${{ }}` expressions, runs its script,
masks secrets in its output, and writes its result: outcome, exit code, outputs (written to `$OCTOPUS_OUTPUT`, or
`$GITHUB_OUTPUT`), and which outputs hold secrets and so stay inside the job. The summary on stderr is made from the
step finished events the runner sends as each step ends.

The job's files live under `<log-root>/state` and are bind mounted into its containers, so the Docker daemon must be
local. `--keep-state` leaves them behind. Build the step runner image first:

```
./build.sh --target PublishPipelinesStepRunner      # builds octopus/pipelines-step-runner:dev
Octopus.Pipelines.Runner.CliSample --workflow workflows/2-different-middle-container.yml --secret DEPLOY_TOKEN=abc
```

## AOT gate

`./build.sh --target CheckPipelinesRunnerAot` publishes with Native AOT, fails on any `warning IL` / `warning AOT`, checks
`-- echo hi` prints `hi`, and prints the median wall time of five `-- true` runs. It publishes for the host's
RID (`linux-x64` or `linux-arm64`), since Native AOT can't link for another architecture without a
cross toolchain.

## Start time

Median start-to-exit for `-- true`: **74 ms** (runs: 70 78 74 67 88 ms), binary 6.9 MB. Measured in the
development sandbox (linux-arm64, local Docker daemon) with the `busybox:1.37` image already pulled
(warm image cache).
