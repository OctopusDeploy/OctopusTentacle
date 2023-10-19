using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha
{
    [Service(typeof(IScriptServiceV3Alpha))]
    public class ScriptServiceV3Alpha : IAsyncScriptServiceV3Alpha
    {
        readonly IScriptServiceV3AlphaExecutor serviceExecutor;

        public ScriptServiceV3Alpha(IScriptServiceV3AlphaExecutor serviceExecutor)
        {
            this.serviceExecutor = serviceExecutor;
        }

        public async Task<ScriptStatusResponseV3Alpha> StartScriptAsync(StartScriptCommandV3Alpha command, CancellationToken cancellationToken)
        {
            if (!serviceExecutor.ValidateExecutionContext(command.ExecutionContext))
                throw new InvalidOperationException($"The execution context type {command.ExecutionContext.GetType().Name} cannot be used with service executor {serviceExecutor.GetType().Name}.");

            return await serviceExecutor.StartScriptAsync(command, cancellationToken);
        }

        public async Task<ScriptStatusResponseV3Alpha> GetStatusAsync(ScriptStatusRequestV3Alpha request, CancellationToken cancellationToken)
            => await serviceExecutor.GetStatusAsync(request, cancellationToken);

        public async Task<ScriptStatusResponseV3Alpha> CancelScriptAsync(CancelScriptCommandV3Alpha command, CancellationToken cancellationToken)
            => await serviceExecutor.CancelScriptAsync(command, cancellationToken);

        public async Task CompleteScriptAsync(CompleteScriptCommandV3Alpha command, CancellationToken cancellationToken)
            => await serviceExecutor.CompleteScriptAsync(command, cancellationToken);

        public bool IsRunningScript(ScriptTicket scriptTicket)
            => serviceExecutor.IsRunningScript(scriptTicket);
    }
}