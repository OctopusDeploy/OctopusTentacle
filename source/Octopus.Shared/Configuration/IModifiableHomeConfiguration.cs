using System;

namespace Octopus.Shared.Configuration
{
    public interface IModifiableHomeConfiguration : IHomeConfiguration
    {
        void SetHomeDirectory(string? homeDirectory);
        void SetCacheDirectory(string cacheDirectory);
    }
}