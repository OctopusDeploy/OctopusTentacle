using System;

namespace Octopus.Shared.Startup
{
    public interface ICommandProcessor
    {
        void Process(string[] args);
    }
}