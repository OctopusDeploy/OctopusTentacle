using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Logs;
using Octopus.Pipelines.Runner.Docker;

const string usage = "usage: Octopus.Pipelines.Runner.CliSample [--image <image>] [--log-root <dir>] -- <command>";

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
var jobDirectory = new Uri(jobDirectoryPath);

var dockerOptions = new DockerBackendOptions();
if (image is not null)
{
    dockerOptions.JobImage = image;
}

using var backend = new DockerJobBackend(dockerOptions);
var logs = new LogStores(new Dictionary<string, ILogStore> { ["file"] = new FileLogStore(root) });
var runner = new JobRunner(backend, logs, new JobRunnerOptions());

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var stdout = Console.OpenStandardOutput();
var result = await runner.RunAsync(new JobSpec(jobId, runId, command), jobDirectory, new StdoutObserver(stdout), cts.Token);

if (result.Error is not null)
{
    Console.Error.WriteLine($"error: {result.Error}");
}

var logPath = Path.Combine(jobDirectoryPath, JobFiles.Log);
Console.Error.WriteLine($"exit={result.ExitCode} log={logPath}");
return result.Error is not null ? 1 : result.ExitCode;

static int Usage()
{
    Console.Error.WriteLine(usage);
    return 2;
}

// Observers must not throw (see IJobObserver), so a closed stdout, e.g. `| head`, stops the echo, not the job. The
// job's log still gets every byte.
sealed class StdoutObserver(Stream stdout) : IJobObserver
{
    bool broken;

    public void Output(ReadOnlySpan<byte> data)
    {
        if (broken)
        {
            return;
        }

        try
        {
            stdout.Write(data);
            stdout.Flush();
        }
        catch (IOException)
        {
            broken = true;
        }
    }
}
