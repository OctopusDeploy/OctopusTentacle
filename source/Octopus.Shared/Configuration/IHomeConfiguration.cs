using System;

namespace Octopus.Shared.Configuration
{
    public interface IHomeConfiguration
    {
        string? ApplicationSpecificHomeDirectory { get; }
        string? HomeDirectory { get; }
        string? CacheDirectory { get; }
    }
}