using System;

namespace Octopus.Shared.Configuration
{
    public interface IHomeConfiguration : IModifiableConfiguration
    {
        string ApplicationSpecificHomeDirectory { get; }
        string HomeDirectory { get; set; }
    }
}