using System;
using Octopus.Shared.Logging;
using Pipefish;

namespace Octopus.Shared.Messages
{
    public interface ICorrelatedMessage : IMessage
    {
        LoggerReference Logger { get; }
    }
}
