using System.Diagnostics;
using System.Text;
using Octopus.Pipelines.Runner.Core;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class ShellJobBackendTests
{
    // The shell starts a child and prints its PID, so the test can tell whether the whole tree was killed.
    internal const string SleepingChild = "sleep 30 & echo $!; wait";

    [Fact]
    public async Task Kills_the_process_tree_when_cancelled()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var output = new PidOutput();
        var backend = new ShellJobBackend(TimeProvider.System);

        var run = backend.RunAsync(new JobSpec("job", "run", SleepingChild), output, cts.Token);
        var pid = await output.Pid.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(ProcessProbe.IsRunning(pid));
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.True(await ProcessProbe.ExitsWithinAsync(pid, TimeSpan.FromSeconds(5)), $"Process {pid} is still running after the run was cancelled.");
    }

    sealed class PidOutput : IJobOutput
    {
        readonly StringBuilder text = new();
        readonly TaskCompletionSource<int> pid = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> Pid => pid.Task;

        public void Started(DateTimeOffset startedAt)
        {
        }

        public void WaitingForNode()
        {
        }

        public void Placed()
        {
        }

        public void Write(ReadOnlySpan<byte> data)
        {
            text.Append(Encoding.UTF8.GetString(data));
            if (ProcessProbe.TryParsePid(text.ToString(), out var value))
            {
                pid.TrySetResult(value);
            }
        }
    }
}

/// <summary>Checks whether a process that isn't a child of the test is still alive.</summary>
static class ProcessProbe
{
    public static bool TryParsePid(string text, out int pid)
    {
        pid = 0;
        var newline = text.IndexOf('\n');
        return newline > 0 && int.TryParse(text.AsSpan(0, newline).Trim(), out pid);
    }

    public static bool IsRunning(int pid)
    {
        if (OperatingSystem.IsLinux())
        {
            // A killed process whose parent also died can linger as a zombie until something reaps it; that's not running.
            try
            {
                var stat = File.ReadAllText($"/proc/{pid}/stat");
                var state = stat[(stat.LastIndexOf(')') + 2)..].Split(' ')[0];
                return state is not ("Z" or "X");
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or IOException)
            {
                return false;
            }
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static async Task<bool> ExitsWithinAsync(int pid, TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();
        while (IsRunning(pid))
        {
            if (deadline.Elapsed > timeout)
            {
                return false;
            }

            await Task.Delay(50);
        }

        return true;
    }
}
