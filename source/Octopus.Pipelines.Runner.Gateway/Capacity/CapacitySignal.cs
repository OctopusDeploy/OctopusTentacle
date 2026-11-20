namespace Octopus.Pipelines.Runner.Gateway;

/// <summary>Wakes the job requester early when capacity may have freed up, e.g. a job finished.</summary>
public sealed class CapacitySignal
{
    readonly SemaphoreSlim signal = new(0, 1);

    public void Notify()
    {
        try
        {
            signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signalled; one wake-up covers any number of changes.
        }
    }

    /// <summary>Waits for <see cref="Notify"/> or the timeout, whichever comes first. True if it was notified.</summary>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => signal.WaitAsync(timeout, cancellationToken);
}
