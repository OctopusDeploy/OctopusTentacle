using System;
using Octopus.Shared.Communications.Logging;
using Pipefish;

namespace Octopus.Core.Orchestration.Messages
{
    public interface IMessageWithLogger : IMessage
    {
        LoggerReference Logger { get; }
    }
}