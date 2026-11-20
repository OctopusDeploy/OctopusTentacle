namespace Octopus.Pipelines.Runner.Core.Logs;

/// <summary>Picks the store from the job directory's URI scheme, so local and Azure share one configuration shape.</summary>
public sealed class LogStores(IReadOnlyDictionary<string, ILogStore> byScheme) : ILogStore
{
    public Task<ILogAppender> CreateAsync(Uri jobDirectory, string fileName, CancellationToken ct) =>
        byScheme.TryGetValue(jobDirectory.Scheme, out var store)
            ? store.CreateAsync(jobDirectory, fileName, ct)
            : throw new NotSupportedException($"No log store is registered for the '{jobDirectory.Scheme}' scheme.");
}
