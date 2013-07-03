using System;

namespace Octopus.Shared.Configuration
{
    public interface IModifiableConfiguraton
    {
        void Save();
    }
}