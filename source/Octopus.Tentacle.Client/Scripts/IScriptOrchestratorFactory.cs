using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Client.Scripts
{
    interface IScriptOrchestratorFactory
    {
        Task<IScriptOrchestrator> CreateOrchestrator(CancellationToken cancellationToken);
    }
}