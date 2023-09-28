using System;
using System.Threading;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class ExecuteScriptCommand : AbstractCommand
    {
        private readonly IScriptWorkspaceFactory workspaceFactory;
        private readonly IScriptExecutorFactory scriptExecutorFactory;
        private readonly Lazy<ShellScriptExecutor> shellScriptExecutor;
        private ScriptTicket? scriptTicket;
        private string? taskId;
        private bool runInShell;

        public ExecuteScriptCommand(ILogFileOnlyLogger logFileOnlyLogger, IScriptWorkspaceFactory workspaceFactory, IScriptExecutorFactory scriptExecutorFactory, Lazy<ShellScriptExecutor> shellScriptExecutor)
            : base(logFileOnlyLogger)
        {
            this.workspaceFactory = workspaceFactory;
            this.scriptExecutorFactory = scriptExecutorFactory;
            this.shellScriptExecutor = shellScriptExecutor;
            Options.Add("scriptTicketId=", "The ScriptTicket Id", v => scriptTicket = new ScriptTicket(v));
            Options.Add("serverTaskId=", "The Server Task Id", v => taskId = v);
            Options.Add("forceShell", "Forces the script to execute in a local shell in all environments", s => runInShell = true);
        }

        protected override void Start()
        {
            if (scriptTicket is null) throw new ArgumentNullException(nameof(scriptTicket), "ScriptTicket Id has not been provided");
            if (taskId is null) throw new ArgumentNullException(nameof(taskId), "The Task Id has not been provided");

            // we always run these under a shell if the flag is set
            var scriptExecutor = runInShell
                ? shellScriptExecutor.Value
                : scriptExecutorFactory.GetExecutor();

            //we can assume the workspace has already been prepared
            var workspace = workspaceFactory.GetWorkspace(scriptTicket);

            //execute the script
            var runningScript = scriptExecutor.Execute(scriptTicket, taskId, workspace, new CancellationTokenSource());

            //if the executed script fails, we need to return that exit code for the entire tentacle result
            if (runningScript.ExitCode != (int)OctopusProgram.ExitCode.Success)
                throw new ScriptExitCodeException(runningScript.ExitCode);
        }
    }
}