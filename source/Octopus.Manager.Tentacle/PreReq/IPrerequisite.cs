using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Manager.Tentacle.PreReq
{
    public interface IPrerequisite
    {
        string StatusMessage { get; }
        Task<PrerequisiteCheckResult> CheckAsync(CancellationToken cancellationToken);
    }
}