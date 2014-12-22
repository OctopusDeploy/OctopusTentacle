using System;

namespace Octopus.Shared.Configuration
{
    public interface IModifiableConfiguration
    {
        void Save();
    }
}