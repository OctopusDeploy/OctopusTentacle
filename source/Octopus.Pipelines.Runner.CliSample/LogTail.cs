namespace Octopus.Pipelines.Runner.CliSample;

/// <summary>
/// Echoes a job's log file as the runner writes it, the way a Server reads job logs from storage rather than from the
/// gateway stream. Output lags by the runner's log flush interval.
/// </summary>
static class LogTail
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>Copies <paramref name="path"/> to <paramref name="destination"/> until <paramref name="done"/> completes, then drains it.</summary>
    public static async Task CopyAsync(string path, Stream destination, Task done)
    {
        FileStream? file = null;
        var echo = true;
        var buffer = new byte[8192];
        try
        {
            while (true)
            {
                // Read the state first: once the job is done its log is complete, so one more pass drains it.
                var finished = done.IsCompleted;
                file ??= TryOpen(path);
                if (file is not null)
                {
                    int read;
                    while ((read = await file.ReadAsync(buffer)) > 0)
                    {
                        echo = echo && TryWrite(destination, buffer.AsSpan(0, read));
                    }
                }

                if (finished)
                {
                    return;
                }

                await Task.WhenAny(done, Task.Delay(PollInterval));
            }
        }
        finally
        {
            if (file is not null)
            {
                await file.DisposeAsync();
            }
        }
    }

    static FileStream? TryOpen(string path)
    {
        try
        {
            // The runner holds the file open for writing while the job runs.
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    // A closed stdout, e.g. `| head`, stops the echo, not the job. The job's log still gets every byte.
    static bool TryWrite(Stream destination, ReadOnlySpan<byte> data)
    {
        try
        {
            destination.Write(data);
            destination.Flush();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
