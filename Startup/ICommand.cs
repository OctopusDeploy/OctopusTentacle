using System;

namespace Octopus.Shared.Startup
{
    public interface ICommand
    {
        OptionSet Options { get; }

        void Start(ICommandRuntime commandRuntime);
        void Stop();
    }
}
