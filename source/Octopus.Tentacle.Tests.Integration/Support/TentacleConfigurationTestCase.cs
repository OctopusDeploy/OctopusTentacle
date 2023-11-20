using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleConfigurationTestCase
    {
        public TentacleType TentacleType { get; }
        public TentacleRuntime TentacleRuntime { get; }
        public Version? Version { get; }
        /// <summary>
        /// The <see cref="Type"/> of the latest script service available on the tentacle version
        /// </summary>
        public Type ScriptServiceTypeToTest { get; }

        public TentacleConfigurationTestCase(TentacleType tentacleType,
            TentacleRuntime tentacleRuntime,
            Version? version,
            Type scriptServiceTypeToTest)
        {
            TentacleType = tentacleType;
            TentacleRuntime = tentacleRuntime;
            Version = version;
            ScriptServiceTypeToTest = scriptServiceTypeToTest;
        }

        internal ClientAndTentacleBuilder CreateBuilder()
        {
            return new ClientAndTentacleBuilder(TentacleType)
                .WithClientOptions(opt =>
                {
                    opt.DisableScriptServiceV3Alpha = ScriptServiceTypeToTest != typeof(IAsyncClientScriptServiceV3Alpha);
                })
                .WithTentacleVersion(Version)
                .WithTentacleRuntime(TentacleRuntime);
        }

        internal LegacyClientAndTentacleBuilder CreateLegacyBuilder()
        {
            return new LegacyClientAndTentacleBuilder(TentacleType)
                .WithTentacleVersion(Version)
                .WithTentacleRuntime(TentacleRuntime);
        }

        public override string ToString()
        {
            StringBuilder builder = new();

            builder.Append($"{TentacleType},");
            var version = Version?.ToString() ?? "Latest";
            builder.Append($"{version},{ScriptServiceTypeToTest.Name.Replace("IAsyncClient", string.Empty)}");

            var tentacleRuntimeDescription = TentacleRuntime.GetDescription();
            var currentRuntime = RuntimeDetection.GetCurrentRuntime();
            if (tentacleRuntimeDescription != currentRuntime)
            {
                builder.Append($",Cl:{currentRuntime},Svc:{tentacleRuntimeDescription}");
            }

            return builder.ToString();
        }
    }
}
