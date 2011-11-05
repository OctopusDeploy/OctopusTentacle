using System;

namespace Octopus.Shared.Startup
{
    public interface IHost
    {
        void RunConsole(Action execute);
        void RunConsoleWithPause(Action execute);
        void RunService(Action execute);
        void RunServiceOrConsole(Action execute);
        void RunServiceOrConsoleWithPause(Action execute);
    }
}