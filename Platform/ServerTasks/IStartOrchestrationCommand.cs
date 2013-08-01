using System;

namespace Octopus.Core.Orchestration.Messages
{
    public interface IStartOrchestrationCommand : IMessageWithLogger
    {
        string TaskId { get; }
    }
}