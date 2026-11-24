# Octopus.Pipelines.Runner.CliSample

Reference for embedding the runner core in a Native AOT .NET CLI that runs jobs in Docker on the same
machine. It references only `Octopus.Pipelines.Runner.Core` and `Octopus.Pipelines.Runner.Docker` and uses no Generic Host.

```
Octopus.Pipelines.Runner.CliSample [--image <image>] [--log-root <dir>] -- <command>
Octopus.Pipelines.Runner.CliSample [--image <image>] [--log-root <dir>] --workflow <file> [--job <id>]
    [--strategy server|run|exec|in-container] [--keep-state]
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

`--workflow` runs one job of a GitHub Actions workflow file: its `container`, `env` and `run` steps with `id`, `name`,
`if` and `env`. Steps write outputs to `$GITHUB_OUTPUT` and later steps read them as
`${{ steps.<id>.outputs.<name> }}`. A step may also set its own `container`, which GitHub Actions doesn't allow on a
`run` step; it overrides the job's. `workflows/` has three samples: every step in the job's container, a different
container for the middle step, and a different container for every step.

`--strategy` picks who orchestrates the steps:

| Strategy | Orchestrated by | Containers | Conditions and expressions |
|---|---|---|---|
| `server` | the Server (here, the CLI) | one `docker run` per step, each its own job | evaluated by the Server before it sends each step |
| `run` (default) | the runner | one `docker run` per step | evaluated by the runner |
| `exec` | the runner | one per image, `docker exec` per step | evaluated by the runner |
| `in-container` | the containers | one per image, all started at once | compiled to `sh`, evaluated in the container |

Every step gets the job's state directory at `/octopus` (scripts, outputs, and the shared `/octopus/workspace`),
bind mounted from `<log-root>/state`, so the Docker daemon must be local. `--keep-state` leaves it behind. The
summary on stderr lists each step's outcome, exit code and outputs.

```
Octopus.Pipelines.Runner.CliSample --workflow workflows/2-different-middle-container.yml --strategy exec
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
