using System;

namespace Octopus.Shared.Startup
{
    public interface IHost
    {
        void RunConsole(Action execute, Action shutdown);
        void RunConsoleWithPause(Action execute, Action shutdown);
        void RunService(Action execute, Action shutdown);
        void RunServiceOrConsole(Action execute, Action shutdown);
        void RunServiceOrConsoleWithPause(Action execute, Action shutdown);
    }
}