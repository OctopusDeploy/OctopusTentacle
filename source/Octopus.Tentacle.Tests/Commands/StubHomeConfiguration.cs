#nullable enable
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Tests.Commands
{
    public class StubHomeConfiguration : IWritableHomeConfiguration
    {
        public string ApplicationSpecificHomeDirectory { get; private set; } = string.Empty;
        public string HomeDirectory { get; set; }= string.Empty;
        public void WriteTo(IWritableKeyValueStore outputStore)
        {
            throw new System.NotImplementedException();
        }

        public string? CacheDirectory { get; set; }

        public void Save()
        {
        }

        public bool SetHomeDirectory(string? homeDirectory)
        {
            throw new System.NotImplementedException();
        }

        public bool SetCacheDirectory(string? cacheDirectory)
        {
            throw new System.NotImplementedException();
        }
    }
}