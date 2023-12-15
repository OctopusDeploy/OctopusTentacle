using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Kubernetes.Scripts
{
    public class KubernetesJobScriptExecutor : IScriptExecutor
    {
        readonly IKubernetesJobService jobService;
        readonly IKubernetesPodService podService;
        readonly IKubernetesJobContainerResolver containerResolver;
        readonly IApplicationInstanceSelector appInstanceSelector;
        readonly ISystemLog log;

        public KubernetesJobScriptExecutor(IKubernetesJobService jobService, IKubernetesPodService podService, IKubernetesJobContainerResolver containerResolver, IApplicationInstanceSelector appInstanceSelector, ISystemLog log)
        {
            this.jobService = jobService;
            this.podService = podService;
            this.containerResolver = containerResolver;
            this.appInstanceSelector = appInstanceSelector;
            this.log = log;
        }

        public bool CanExecute(StartScriptCommandV3Alpha command) => command.ExecutionContext is KubernetesJobScriptExecutionContext;

        public IRunningScript ExecuteOnBackgroundThread(StartScriptCommandV3Alpha command, IScriptWorkspace workspace, ScriptStateStore scriptStateStore, CancellationToken cancellationToken)
        {
            var runningScript = new RunningKubernetesJob(workspace, workspace.CreateLog(), command.ScriptTicket, command.TaskId, cancellationToken, log, scriptStateStore, jobService, podService, containerResolver, appInstanceSelector);

            Task.Run(() => runningScript.Execute(), cancellationToken);

            return runningScript;
        }
    }
}