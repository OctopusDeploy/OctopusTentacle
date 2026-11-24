using System.ComponentModel;
using System.Diagnostics;

namespace Octopus.Pipelines.Runner.Docker;

/// <summary>
/// Runs the <c>docker</c> CLI. The CLI resolves the daemon (<c>DOCKER_HOST</c>, the current context) and registry
/// credentials (<c>config.json</c>, credential helpers) exactly as it does for the user running the runner.
/// </summary>
sealed class DockerCli(string executable)
{
    /// <summary>Runs a command to completion and captures its output. Cancelling kills the CLI process.</summary>
    public async Task<CliResult> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        using var process = Start(args);
        using var kill = cancellationToken.Register(static state => Kill((Process)state!), process);
        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        await process.WaitForExitAsync(CancellationToken.None);
        var result = new CliResult(args, process.ExitCode, await stdout, await stderr);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Starts a command with stdout and stderr redirected and stdin closed. The caller owns the process.</summary>
    public Process Start(IReadOnlyList<string> args)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Couldn't start '{executable}'.");
        }
        catch (Win32Exception e)
        {
            throw new InvalidOperationException($"Couldn't run '{executable}': {e.Message}. The runner needs the Docker CLI on the PATH.", e);
        }

        // Nothing is ever sent to a job, and a CLI that inherited a terminal's stdin could otherwise read from it.
        process.StandardInput.Close();
        return process;
    }

    public static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception)
        {
            // Already exited.
        }
    }
}

sealed record CliResult(IReadOnlyList<string> Args, int ExitCode, string Stdout, string Stderr)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>Throws with the CLI's own error message unless the command succeeded.</summary>
    public CliResult EnsureSuccess()
    {
        if (!Succeeded)
        {
            var message = Stderr.Trim() is { Length: > 0 } stderr ? stderr : Stdout.Trim();
            throw new InvalidOperationException($"docker {Args[0]} failed with exit code {ExitCode}: {message}");
        }

        return this;
    }
}
