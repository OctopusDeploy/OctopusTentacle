using System;
using Octopus.Shared.Platform.Logging;
using Pipefish;

namespace Octopus.Shared.Platform
{
    public interface IMessageWithLogger : IMessage
    {
        LoggerReference Logger { get; }
    }
}