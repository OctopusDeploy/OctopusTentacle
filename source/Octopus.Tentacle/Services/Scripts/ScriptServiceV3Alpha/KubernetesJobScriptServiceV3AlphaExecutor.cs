using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Scripts.Kubernetes;

namespace Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha
{
    public class KubernetesJobScriptServiceV3AlphaExecutor : ScriptServiceV3AlphaExecutor
    {
        readonly IKubernetesJobService jobService;
        readonly IApplicationInstanceSelector appInstanceSelector;
        readonly ISystemLog log;

        public KubernetesJobScriptServiceV3AlphaExecutor(
            IScriptWorkspaceFactory workspaceFactory,
            IScriptStateStoreFactory scriptStateStoreFactory,
            IKubernetesJobService jobService,
            IApplicationInstanceSelector appInstanceSelector,
            ISystemLog log)
            : base(workspaceFactory, scriptStateStoreFactory)
        {
            this.jobService = jobService;
            this.appInstanceSelector = appInstanceSelector;
            this.log = log;
        }

        public override bool ValidateExecutionContext(IScriptExecutionContext executionContext)
            => executionContext is KubernetesJobScriptExecutionContext;

        protected override IRunningScript ExecuteScript(StartScriptCommandV3Alpha startScriptCommand, IScriptWorkspace workspace, ScriptStateStore scriptStateStore, CancellationToken cancellationToken)
        {
            if (startScriptCommand.ExecutionContext is not KubernetesJobScriptExecutionContext kubernetesJobScriptExecutionContext)
                throw new InvalidOperationException("The ExecutionContext must be of type KubernetesJobScriptExecutionContext");

            var runningScript = new RunningKubernetesJobScript(workspace, workspace.CreateLog(), startScriptCommand.ScriptTicket, startScriptCommand.TaskId, cancellationToken, log, jobService, appInstanceSelector, kubernetesJobScriptExecutionContext);

            Task.Run(async () =>
            {
                await runningScript.Execute(cancellationToken);
            }, cancellationToken);

            return runningScript;
        }
    }
}