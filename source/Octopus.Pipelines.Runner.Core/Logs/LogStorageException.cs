namespace Octopus.Pipelines.Runner.Core.Logs;

/// <summary>
/// A log storage failure. Appenders throw it with <paramref name="permanent"/> set when retrying can't
/// help (an expired or revoked grant, a deleted blob, a denied file); any other exception from an
/// appender is treated as transient.
/// </summary>
public sealed class LogStorageException(string message, bool permanent, Exception? inner = null) : Exception(message, inner)
{
    public bool Permanent { get; } = permanent;
}
