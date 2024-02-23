using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Kubernetes.Scripts
{
    public class KubernetesPodScriptExecutor : IScriptExecutor
    {
        readonly IKubernetesPodService podService;
        readonly IKubernetesSecretService secretService;
        readonly IKubernetesPodContainerResolver containerResolver;
        readonly IApplicationInstanceSelector appInstanceSelector;
        readonly ISystemLog log;

        public KubernetesPodScriptExecutor(IKubernetesPodService podService, IKubernetesSecretService secretService, IKubernetesPodContainerResolver containerResolver, IApplicationInstanceSelector appInstanceSelector, ISystemLog log)
        {
            this.podService = podService;
            this.secretService = secretService;
            this.containerResolver = containerResolver;
            this.appInstanceSelector = appInstanceSelector;
            this.log = log;
        }

        public bool CanExecute(StartScriptCommandV3Alpha command) => command.ExecutionContext is KubernetesAgentScriptExecutionContext;

        public IRunningScript ExecuteOnBackgroundThread(StartScriptCommandV3Alpha command, IScriptWorkspace workspace, ScriptStateStore scriptStateStore, CancellationToken cancellationToken)
        {
            var runningScript = new RunningKubernetesPod(
                workspace,
                workspace.CreateLog(),
                command.ScriptTicket,
                command.TaskId, log,
                scriptStateStore,
                podService,
                secretService,
                containerResolver,
                appInstanceSelector,
                (KubernetesAgentScriptExecutionContext)command.ExecutionContext,
                cancellationToken);

            Task.Run(() => runningScript.Execute(), cancellationToken);

            return runningScript;
        }
    }
}