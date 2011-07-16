using System;

namespace Octopus.Shared.Configuration
{
    public interface IGlobalConfiguration
    {
        string Get(string name);
        void Set(string name, string value);
    }
}