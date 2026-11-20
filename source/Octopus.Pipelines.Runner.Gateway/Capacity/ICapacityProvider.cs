namespace Octopus.Pipelines.Runner.Gateway;

/// <summary>
/// Decides how much work this runner pulls from the Server. The runner asks this before every
/// AllocateNextJobCommand, after each job finishes and on <see cref="GatewayOptions.CapacityCheckInterval"/>.
/// </summary>
/// <remarks>
/// Implementations can look at anything the runner can observe, e.g. pending pods or free node
/// resources in the cell's cluster, so a busy cluster simply stops pulling.
/// </remarks>
public interface ICapacityProvider
{
    /// <summary>
    /// How many more jobs this runner could start right now, counting jobs it is already running
    /// but not ones it has asked for and not yet received. Zero or less means "don't ask for more".
    /// </summary>
    ValueTask<int> GetAvailableAsync(CancellationToken cancellationToken);
}
