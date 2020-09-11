using System;

namespace Octopus.Shared.Configuration
{
    public interface IHomeConfiguration
    {
        string? ApplicationSpecificHomeDirectory { get; }
        string? HomeDirectory { get; set; }
        string? CacheDirectory { get; set; }
    }
}