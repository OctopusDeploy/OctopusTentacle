using System;

namespace Octopus.Shared.Contracts
{
    public interface ITaskService
    {
        TaskTicket RunScript(RunScriptRequest request);
        TaskTicket DeployPackage();

        TaskStatusResponse GetStatus(TaskStatusRequest request);
        TaskStatusResponse CancelTask(TaskCancelRequest request);
        TaskStatusResponse CompleteTask(TaskCompleteRequest request);
    }
}