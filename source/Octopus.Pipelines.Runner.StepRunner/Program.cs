using System.Runtime.InteropServices;
using Octopus.Pipelines.Runner.StepRunner;

// The entry point of a step's container. The job runner passes everything through environment variables (see
// StepEnvironment); `install <dir>` copies this executable into <dir>, for a Kubernetes init container.

if (args is ["install", var directory])
{
    return Installer.Install(directory);
}

if (args.Length > 0)
{
    Console.Error.WriteLine("usage: octopus-step-runner [install <dir>]");
    return 2;
}

using var stop = new CancellationTokenSource();

// The job runner cancels a step by stopping its container, which sends SIGTERM (SIGINT for a terminal). Stop the
// script and still write a result, rather than dying with it.
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Cancel);
using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, Cancel);

using var stdout = Console.OpenStandardOutput();
return await new StepRunnerApp(Environment.GetEnvironmentVariables(), stdout, TimeProvider.System).RunAsync(stop.Token);

void Cancel(PosixSignalContext context)
{
    context.Cancel = true;
    stop.Cancel();
}
