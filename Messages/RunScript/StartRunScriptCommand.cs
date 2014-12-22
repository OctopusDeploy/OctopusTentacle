using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Deploy.Script;

namespace Octopus.Platform.Deployment.Messages.RunScript
{
    public class StartRunScriptCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public string TaskId { get; private set; }
        public TimeSpan Timeout { get; private set; }
        public string ScriptBody { get; private set; }
        public string[] EnvironmentIds { get; private set; }
        public string[] TargetRoles { get; private set; }
        public string[] MachineIds { get; private set; }
        public ScriptSyntax Syntax { get; private set; }

        public StartRunScriptCommand(
            LoggerReference logger,
            string taskId,
            TimeSpan timeout,
            string scriptBody,
            ScriptSyntax syntax,
            string[] environmentIds = null,
            string[] targetRoles = null,
            string[] machineIds = null)
        {
            Logger = logger;
            TaskId = taskId;
            Timeout = timeout;
            ScriptBody = scriptBody;
            Syntax = syntax;
            EnvironmentIds = environmentIds;
            TargetRoles = targetRoles;
            MachineIds = machineIds;
        }
    }
}