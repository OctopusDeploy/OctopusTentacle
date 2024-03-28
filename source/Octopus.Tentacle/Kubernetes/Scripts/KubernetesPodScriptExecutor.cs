using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Kubernetes.Scripts
{
    public class KubernetesPodScriptExecutor
    {
        readonly RunningKubernetesPod.Factory runningKubernetesPodFactory;

        public KubernetesPodScriptExecutor(RunningKubernetesPod.Factory runningKubernetesPodFactory)
        {
            this.runningKubernetesPodFactory = runningKubernetesPodFactory;
        }

        public IRunningScript ExecuteOnBackgroundThread(StartKubernetesScriptCommandV1Alpha command, IScriptWorkspace workspace, ScriptStateStore scriptStateStore, CancellationToken cancellationToken)
        {
            var runningScript = runningKubernetesPodFactory(
                workspace,
                workspace.CreateLog(),
                command,
                scriptStateStore,
                cancellationToken);

            Task.Run(() => runningScript.Execute(), cancellationToken);

            return runningScript;
        }
    }
}