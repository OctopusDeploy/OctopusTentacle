using System;

namespace Octopus.Shared.Startup
{
    public interface ICommandHost
    {
        void Run(Action<ICommandRuntime> start, Action shutdown);
        void Stop(Action shutdown);
        void OnExit(int exitCode);
    }
}