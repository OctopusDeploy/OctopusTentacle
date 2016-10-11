using System;
using Octopus.Server.Extensibility.HostServices.Configuration;

namespace Octopus.Shared.Configuration
{
    public interface IHomeConfiguration : IModifiableConfiguration
    {
        string ApplicationSpecificHomeDirectory { get; }
        string HomeDirectory { get; set; }
    }
}