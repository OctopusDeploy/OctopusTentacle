using System;

namespace Octopus.Shared.Startup
{
    public interface ICommandOptions
    {
        void Validate();
    }
}