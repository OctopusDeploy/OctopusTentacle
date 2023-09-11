using System;
using System.Text;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleConfigurationTestCase
    {
        public TentacleType TentacleType { get; }
        public SyncOrAsyncHalibut SyncOrAsyncHalibut { get; }
        public TentacleRuntime TentacleRuntime { get; }
        public Version? Version { get; }

        public TentacleConfigurationTestCase(
            TentacleType tentacleType,
            SyncOrAsyncHalibut syncOrAsyncHalibut,
            TentacleRuntime tentacleRuntime,
            Version? version)
        {
            TentacleType = tentacleType;
            SyncOrAsyncHalibut = syncOrAsyncHalibut;
            TentacleRuntime = tentacleRuntime;
            Version = version;
        }
        
        internal ClientAndTentacleBuilder CreateBuilder()
        {
            return new ClientAndTentacleBuilder(TentacleType)
                .WithAsyncHalibutFeature(SyncOrAsyncHalibut.ToAsyncHalibutFeature())
                .WithTentacleVersion(Version)
                .WithTentacleRuntime(TentacleRuntime);
        }

        internal LegacyClientAndTentacleBuilder CreateLegacyBuilder()
        {
            return new LegacyClientAndTentacleBuilder(TentacleType)
                .WithAsyncHalibutFeature(SyncOrAsyncHalibut.ToAsyncHalibutFeature())
                .WithTentacleVersion(Version)
                .WithTentacleRuntime(TentacleRuntime);
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            
            builder.Append($"{TentacleType},");
            string version = Version?.ToString() ?? "Latest";
            builder.Append($"{version},");
            builder.Append($"{SyncOrAsyncHalibut}");

            if (TentacleRuntime != TentacleRuntime.Default)
            {
                builder.Append($",{TentacleRuntime.GetDescription()}");
            }

            return builder.ToString();
        }
    }
}
