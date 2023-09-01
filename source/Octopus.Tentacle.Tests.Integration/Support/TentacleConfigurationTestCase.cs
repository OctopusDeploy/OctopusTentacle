using System;
using System.Text;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleConfigurationTestCase
    {
        public TentacleType TentacleType { get; }
        public SyncOrAsyncHalibut SyncOrAsyncHalibut { get; }
        public Version? Version { get; }

        public TentacleConfigurationTestCase(
            TentacleType tentacleType,
            SyncOrAsyncHalibut syncOrAsyncHalibut,
            Version? version)
        {
            TentacleType = tentacleType;
            SyncOrAsyncHalibut = syncOrAsyncHalibut;
            Version = version;
        }
        
        internal ClientAndTentacleBuilder CreateBuilder()
        {
            return new ClientAndTentacleBuilder(TentacleType)
                .WithAsyncHalibutFeature(SyncOrAsyncHalibut.ToAsyncHalibutFeature())
                .WithTentacleVersion(Version);
        }

        internal LegacyClientAndTentacleBuilder CreateLegacyBuilder()
        {
            return new LegacyClientAndTentacleBuilder(TentacleType)
                .WithAsyncHalibutFeature(SyncOrAsyncHalibut.ToAsyncHalibutFeature())
                .WithTentacleVersion(Version);
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            
            builder.Append($"{TentacleType},");
            builder.Append($"{SyncOrAsyncHalibut},");
            
            string version = Version?.ToString() ?? "Latest";
            builder.Append($"{version}");

            return builder.ToString();
        }
    }
}
