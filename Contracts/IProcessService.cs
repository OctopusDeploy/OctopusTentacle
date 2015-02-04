using System;

namespace Octopus.Shared.Contracts
{
    public interface IProcessService
    {
        ProcessTicket RunProcess(RunProcessCommand command);
        ProcessStatusResponse GetStatus(ProcessStatusRequest request);
        ProcessStatusResponse CancelProcess(CancelProcessCommand command);
        ProcessStatusResponse CompleteProcess(CompleteProcessCommand command);
    }
}