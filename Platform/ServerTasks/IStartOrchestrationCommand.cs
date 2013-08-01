using System;

namespace Octopus.Shared.Platform.ServerTasks
{
    public interface IStartOrchestrationCommand : IMessageWithLogger
    {
        string TaskId { get; }
    }
}