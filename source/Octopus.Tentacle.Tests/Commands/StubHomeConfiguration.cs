using Octopus.Shared.Configuration;

namespace Octopus.Tentacle.Tests.Commands
{
    public class StubHomeConfiguration : IHomeConfiguration
    {
        public string ApplicationSpecificHomeDirectory { get; private set; }
        public string HomeDirectory { get; set; }
        public string CacheDirectory { get; set; }

        public void Save()
        {
        }
    }
}