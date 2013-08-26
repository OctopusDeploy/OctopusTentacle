using System;
using System.Collections.Generic;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Guidance
{
    public class FailureGuidanceRequest : IMessageWithLogger
    {
        public LoggerReference Logger { get; private set; }
        public string Prompt { get; private set; }
        public List<FailureGuidance> SupportedActions { get; set; }

        public FailureGuidanceRequest(LoggerReference logger, string prompt, List<FailureGuidance> supportedActions)
        {
            Logger = logger;
            Prompt = prompt;
            SupportedActions = supportedActions;
        }
    }
}
