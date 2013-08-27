using System;
using System.Collections.Generic;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Guidance
{
    public class FailureGuidanceRequest : IMessageWithLogger
    {
        public LoggerReference Logger { get; private set; }
        public string TaskId { get; private set; }
        public string DeploymentId { get; private set; }
        public string Prompt { get; private set; }
        public List<FailureGuidance> SupportedActions { get; private set; }

        public FailureGuidanceRequest(
            LoggerReference logger,
            string taskId,
            string deploymentId,
            string prompt,
            List<FailureGuidance> supportedActions)
        {
            Logger = logger;
            TaskId = taskId;
            DeploymentId = deploymentId;
            Prompt = prompt;
            SupportedActions = supportedActions;
        }
    }
}
