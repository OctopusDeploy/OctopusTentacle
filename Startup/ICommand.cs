using System;

namespace Octopus.Shared.Startup
{
    public interface ICommand
    {
        OptionSet Options { get; }

        void Execute();
    }
}
