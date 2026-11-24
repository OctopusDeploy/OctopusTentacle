using System.Diagnostics;

namespace Octopus.Pipelines.Runner.Core;

/// <summary>Runs jobs as local processes. Only for local development; it gives no isolation.</summary>
public sealed class ProcessJobBackend(TimeProvider clock) : IJobBackend
{
    public async Task<int> RunAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
    {
        if (job.Sandboxed)
        {
            // Never run a job that asked for a sandbox without one.
            throw new NotSupportedException("Sandboxed jobs need the Kubernetes backend with Runner:Kubernetes:Sandbox:Enabled.");
        }

        if (job.Steps.Count > 0)
        {
            throw new NotSupportedException("The process backend doesn't run jobs with steps; use the Docker backend.");
        }

        var startInfo = new ProcessStartInfo("sh")
        {
            ArgumentList = { "-c", job.Command },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start sh.");

        // Every cancellation path kills the whole tree, including while the pumps are still reading: a job must
        // never outlive its run. Registered before anything else can observe the token.
        using var kill = cancellationToken.Register(static state => Kill((Process)state!), process);
        output.Started(clock.GetUtcNow());

        // stdout and stderr are interleaved at chunk granularity, like a container log.
        var gate = new Lock();
        await Task.WhenAll(
            PumpAsync(process.StandardOutput.BaseStream, output, gate, cancellationToken),
            PumpAsync(process.StandardError.BaseStream, output, gate, cancellationToken));
        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode;
    }

    static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already exited.
        }
    }

    static async Task PumpAsync(Stream source, IJobOutput output, Lock gate, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            lock (gate)
            {
                output.Write(buffer.AsSpan(0, read));
            }
        }
    }
}
