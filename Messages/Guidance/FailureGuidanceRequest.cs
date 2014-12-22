using System;
using System.Collections.Generic;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;

namespace Octopus.Shared.Messages.Guidance
{
    [ExpectReply]
    public class FailureGuidanceRequest : ICorrelatedMessage
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
