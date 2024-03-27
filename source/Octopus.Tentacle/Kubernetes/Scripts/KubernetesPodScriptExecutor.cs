using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Kubernetes.Scripts
{
    public class KubernetesPodScriptExecutor : IScriptExecutor
    {
        readonly RunningKubernetesPod.Factory runningKubernetesPodFactory;
        readonly SensitiveValueMasker sensitiveValueMasker;
        readonly IKubernetesLogService kubernetesLogService;

        public KubernetesPodScriptExecutor(RunningKubernetesPod.Factory runningKubernetesPodFactory, SensitiveValueMasker sensitiveValueMasker, IKubernetesLogService kubernetesLogService)
        {
            this.runningKubernetesPodFactory = runningKubernetesPodFactory;
            this.sensitiveValueMasker = sensitiveValueMasker;
            this.kubernetesLogService = kubernetesLogService;
        }

        public bool CanExecute(StartScriptCommandV3Alpha command) => command.ExecutionContext is KubernetesAgentScriptExecutionContext;

        public IRunningScript ExecuteOnBackgroundThread(StartScriptCommandV3Alpha command, IScriptWorkspace workspace, ScriptStateStore scriptStateStore, CancellationToken cancellationToken)
        {
            var runningScript = runningKubernetesPodFactory(
                workspace,
                new KubernetesScriptLog( kubernetesLogService,sensitiveValueMasker, command.ScriptTicket),
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