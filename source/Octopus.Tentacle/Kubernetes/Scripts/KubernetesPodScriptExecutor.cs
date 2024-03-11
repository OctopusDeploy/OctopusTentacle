using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Kubernetes.Scripts
{
    public class KubernetesPodScriptExecutor : IScriptExecutor
    {
        readonly RunningKubernetesPod.Factory runningKubernetesPodFactory;

        public KubernetesPodScriptExecutor(RunningKubernetesPod.Factory runningKubernetesPodFactory)
        {
            this.runningKubernetesPodFactory = runningKubernetesPodFactory;
        }

        public bool CanExecute(StartScriptCommandV3Alpha command) => command.ExecutionContext is KubernetesAgentScriptExecutionContext;

        public IRunningScript ExecuteOnBackgroundThread(StartScriptCommandV3Alpha command, IScriptWorkspace workspace, ScriptStateStore scriptStateStore, CancellationToken cancellationToken)
        {
            var runningScript = runningKubernetesPodFactory(
                workspace,
                workspace.CreateLog(),
                command.ScriptTicket,
                command.TaskId,
                scriptStateStore,
                (KubernetesAgentScriptExecutionContext)command.ExecutionContext,
                cancellationToken);

            Task.Run(() => runningScript.Execute(), cancellationToken);

            return runningScript;
        }
    }
}