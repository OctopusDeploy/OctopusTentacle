using System;
using System.Collections.Generic;
using Octopus.Platform.Deployment.Logging;

namespace Octopus.Platform.Deployment.Messages.Deploy.Steps
{
    public class RunStepCommand : IReusableMessage
    {
        public LoggerReference Logger { get; private set; }
        public string DeploymentId { get; set; }
        public string StepId { get; set; }
        public List<StepAction> Actions { get; set; }
        public string MachineId { get; set; }
        public string MachineSquid { get; set; }

        public RunStepCommand(LoggerReference logger, string deploymentId, string stepId, List<StepAction> actions, string machineId = null)
        {
            Logger = logger;
            DeploymentId = deploymentId;
            StepId = stepId;
            Actions = actions;
            MachineId = machineId;
        }

        public IReusableMessage CopyForReuse(LoggerReference newLogger)
        {
            return new RunStepCommand(newLogger, DeploymentId, StepId, Actions, MachineId);
        }
    }
}