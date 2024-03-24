using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Scripts.Execution;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Client.Scripts
{
    class ScriptServiceV1Orchestrator : ObservingScriptOrchestrator
    {
        readonly IScriptServiceExecutor scriptServiceExecutor;

        public ScriptServiceV1Orchestrator(
            IScriptServiceExecutor scriptServiceExecutor,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            TentacleClientOptions clientOptions)
            : base(scriptObserverBackOffStrategy,
                onScriptStatusResponseReceived,
                onScriptCompleted,
                clientOptions)
        {
            this.scriptServiceExecutor = scriptServiceExecutor;
        }
        
        protected override async Task<ScriptStatusResponseV3Alpha> StartScript(StartScriptCommandV3Alpha command, CancellationToken scriptExecutionCancellationToken)
        {
            return await scriptServiceExecutor.StartScript(command, scriptExecutionCancellationToken);
        }

        protected override async Task<ScriptStatusResponseV3Alpha> GetStatus(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return await scriptServiceExecutor.GetStatus(lastStatusResponse, scriptExecutionCancellationToken);
        }

        protected override async Task<ScriptStatusResponseV3Alpha> Cancel(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return await scriptServiceExecutor.Cancel(lastStatusResponse, scriptExecutionCancellationToken);
        }

        protected override async Task<ScriptStatusResponseV3Alpha> Finish(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            var response = await scriptServiceExecutor.Finish(lastStatusResponse, scriptExecutionCancellationToken);

            OnScriptStatusResponseReceived(response);

            return response;
        }
    }
}