using System;
using Octopus.Platform.Deployment.Logging;
using Pipefish;

namespace Octopus.Platform.Deployment.Messages
{
    public interface ICorrelatedMessage : IMessage
    {
        LoggerReference Logger { get; }
    }
}
