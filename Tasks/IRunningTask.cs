using System;

namespace Octopus.Shared.Tasks
{
    public interface IRunningTask
    {
        void Start();
        void Cancel();
    }
}