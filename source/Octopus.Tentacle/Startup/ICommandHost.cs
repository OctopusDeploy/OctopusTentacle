using System;

namespace Octopus.Tentacle.Startup
{
    public interface ICommandHost
    {
        void Run(Action<ICommandRuntime> start, Action shutdown);
        void Stop(Action shutdown);
        void OnExit(int exitCode);
    }
}