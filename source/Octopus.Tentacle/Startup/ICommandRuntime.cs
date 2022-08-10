using System;

namespace Octopus.Tentacle.Startup
{
    public interface ICommandRuntime
    {
        void WaitForUserToExit();
    }
}