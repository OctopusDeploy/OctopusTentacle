#nullable enable
using System;
using Octopus.Shared.Configuration;

namespace Octopus.Tentacle.Tests.Commands
{
    public class StubHomeConfiguration : IWritableHomeConfiguration
    {
        public string ApplicationSpecificHomeDirectory { get; } = string.Empty;
        public string HomeDirectory { get; set; } = string.Empty;
        public string? CacheDirectory { get; set; }

        public void Save()
        {
        }

        public bool SetHomeDirectory(string? homeDirectory)
        {
            throw new NotImplementedException();
        }

        public bool SetCacheDirectory(string? cacheDirectory)
        {
            throw new NotImplementedException();
        }
    }
}