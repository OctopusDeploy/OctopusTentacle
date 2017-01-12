using System;
using System.Threading;

namespace Octopus.Shared.Tasks
{
    public interface ITaskController
    {
        void Execute();
        ThreadPriority ExecutionPriority { get; }
    }
}