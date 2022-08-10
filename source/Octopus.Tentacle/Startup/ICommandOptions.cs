using System;

namespace Octopus.Tentacle.Startup
{
    public interface ICommandOptions
    {
        void Validate();
    }
}