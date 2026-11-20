namespace Octopus.Pipelines.Runner.Core;

public sealed class JobRunnerOptions
{
    /// <summary>How often buffered log output is appended to the log store.</summary>
    public TimeSpan LogFlushInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Buffered log bytes that trigger an early flush.</summary>
    public int LogFlushBytes { get; set; } = 64 * 1024;

    /// <summary>Unflushed log bytes beyond which the job is failed.</summary>
    public long MaxPendingLogBytes { get; set; } = 64L * 1024 * 1024;
}
