using System;

namespace Octopus.Shared.Startup
{
    public interface ICommandRuntime
    {
        void WaitForUserToExit();
    }
}