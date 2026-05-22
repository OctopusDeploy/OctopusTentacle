using System;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Startup
{
    public interface ICommandHost
    {
        void Run(Action<ICommandRuntime> start, Action shutdown);
        Task RunAsync(Func<ICommandRuntime, Task> start, Action shutdown);
        void Stop(Action shutdown);
        void OnExit(int exitCode);
    }
}