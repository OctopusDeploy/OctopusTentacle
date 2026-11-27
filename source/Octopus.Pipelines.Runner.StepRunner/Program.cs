using System.Collections;
using System.Runtime.InteropServices;
using Octopus.Pipelines.Runner.StepRunner;

// The step runner, installed into each of a job's containers:
//   run [KEY=VALUE]...   runs one step. The job runner passes OCTOPUS_* as arguments (a Kubernetes exec can't set
//                        environment variables); the environment is the fallback.
//   idle                 keeps the job's container alive between steps, until it's stopped.
//   install <dir>        copies this executable into <dir>, for a Kubernetes init container.

using var stop = new CancellationTokenSource();

// Stopping the container sends SIGTERM (SIGINT for a terminal): stop the script, and still write a result.
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Cancel);
using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, Cancel);

switch (args)
{
    case ["install", var directory]:
        return Installer.Install(directory);

    case ["idle"]:
        try
        {
            await Task.Delay(Timeout.Infinite, stop.Token);
        }
        catch (OperationCanceledException)
        {
        }

        return 0;

    case [] or ["run", ..]:
        var environment = new Hashtable(Environment.GetEnvironmentVariables());
        foreach (var setting in args.Skip(1))
        {
            var equals = setting.IndexOf('=');
            if (equals <= 0)
            {
                return Usage();
            }

            environment[setting[..equals]] = setting[(equals + 1)..];
        }

        using (var stdout = Console.OpenStandardOutput())
        {
            return await new StepRunnerApp(environment, stdout, TimeProvider.System).RunAsync(stop.Token);
        }

    default:
        return Usage();
}

void Cancel(PosixSignalContext context)
{
    context.Cancel = true;
    stop.Cancel();
}

static int Usage()
{
    Console.Error.WriteLine("usage: octopus-step-runner run [KEY=VALUE]... | idle | install <dir>");
    return 2;
}
