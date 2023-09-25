using System;
using System.Threading;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class ExecuteK8sJobScriptCommand : AbstractCommand
    {
        private readonly IScriptWorkspaceFactory workspaceFactory;
        private readonly IScriptExecutorFactory scriptExecutorFactory;
        private ScriptTicket? ticket;
        private string? taskId;

        public ExecuteK8sJobScriptCommand(ILogFileOnlyLogger logFileOnlyLogger, IScriptWorkspaceFactory workspaceFactory, IScriptExecutorFactory scriptExecutorFactory) : base(logFileOnlyLogger)
        {
            this.workspaceFactory = workspaceFactory;
            this.scriptExecutorFactory = scriptExecutorFactory;
            Options.Add("t|ticket-id", "The ScriptTicket Id", v => ticket = new ScriptTicket(v));
            Options.Add("task-id", "The Server Task Id", v => taskId = v);
        }

        protected override void Start()
        {
            if (ticket is null) throw new ArgumentNullException(nameof(ticket), "ScriptTicket Id has not been provided");
            if (taskId is null) throw new ArgumentNullException(nameof(taskId), "The Task Id has not been provided");

            // we always run these under a shell
            var scriptExecutor = scriptExecutorFactory.GetExecutor(ScriptExecutor.Shell);

            //we can assume the workspace has already been prepared
            var workspace = workspaceFactory.GetWorkspace(ticket);

            //execute the script
            var runningScript = scriptExecutor.Execute(ticket, taskId, workspace, new CancellationTokenSource(), true);

            //if the executed script fails, we need to return that exit code for the entire tentacle result
            if (runningScript.ExitCode != (int)OctopusProgram.ExitCode.Success)
                throw new ScriptExitCodeException(runningScript.ExitCode);
        }
    }
}