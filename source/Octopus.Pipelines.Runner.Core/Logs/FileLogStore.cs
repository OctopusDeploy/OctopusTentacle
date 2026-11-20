namespace Octopus.Pipelines.Runner.Core.Logs;

/// <summary>
/// Writes job files to local disk, under <paramref name="root"/>. For local development, tests and
/// self-hosted runners. Refuses any target outside the root.
/// </summary>
public sealed class FileLogStore(string root) : ILogStore
{
    readonly string root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    public Task<ILogAppender> CreateAsync(Uri jobDirectory, string fileName, CancellationToken ct)
    {
        if (!jobDirectory.IsFile)
        {
            throw new ArgumentException($"Not a file URI: {jobDirectory}", nameof(jobDirectory));
        }

        if (fileName.Length == 0 || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
        {
            throw new ArgumentException($"Invalid file name: {fileName}", nameof(fileName));
        }

        var fullPath = Path.GetFullPath(jobDirectory.LocalPath + "/" + fileName);
        if (!fullPath.StartsWith(root, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Job directory is outside the log root: {jobDirectory}", nameof(jobDirectory));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, bufferSize: 0, useAsync: true);
        return Task.FromResult<ILogAppender>(new Appender(stream));
    }

    sealed class Appender(FileStream stream) : ILogAppender
    {
        public long Length { get; private set; }

        public async Task AppendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
        {
            try
            {
                await stream.WriteAsync(data, ct);
                await stream.FlushAsync(ct);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new LogStorageException("Permission denied writing the log file.", permanent: true, ex);
            }

            Length += data.Length;
        }

        public ValueTask DisposeAsync() => stream.DisposeAsync();
    }
}
