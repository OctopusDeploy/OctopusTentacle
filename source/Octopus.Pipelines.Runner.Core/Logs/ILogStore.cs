namespace Octopus.Pipelines.Runner.Core.Logs;

/// <summary>Append-only writer for a single job's log. Not thread-safe: one writer per job.</summary>
public interface ILogAppender : IAsyncDisposable
{
    /// <summary>Bytes durably written so far.</summary>
    long Length { get; }

    Task AppendAsync(ReadOnlyMemory<byte> data, CancellationToken ct);
}

/// <summary>Where a job's files go: a store creates them inside the job's directory.</summary>
public interface ILogStore
{
    /// <summary>
    /// Creates an empty file in the job's directory and returns an appender for it. Fails if the file
    /// already exists: a job's log is never overwritten.
    /// </summary>
    /// <param name="jobDirectory">
    /// The job's directory: <c>https://account.blob.core.windows.net/c-customer/runs/run/job?sas</c> or
    /// <c>file:///root/c-customer/runs/run/job</c>.
    /// </param>
    Task<ILogAppender> CreateAsync(Uri jobDirectory, string fileName, CancellationToken ct);
}

public static class JobDirectory
{
    /// <summary>The URI of a file inside a job's directory. The query (the SAS) is kept exactly as given.</summary>
    public static Uri FileUri(Uri jobDirectory, string fileName)
    {
        ArgumentNullException.ThrowIfNull(jobDirectory);
        return new Uri($"{jobDirectory.GetLeftPart(UriPartial.Path).TrimEnd('/')}/{fileName}{jobDirectory.Query}");
    }
}
