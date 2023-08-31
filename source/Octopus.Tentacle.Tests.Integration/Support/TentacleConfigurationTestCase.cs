using System;

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
    }

    public class TentacleConfigurationWithRpcCallStageTestCase : TentacleConfigurationTestCase
    {
        public RpcCallStage RpcCallStage { get; }

        public TentacleConfigurationWithRpcCallStageTestCase(
            TentacleType tentacleType,
            SyncOrAsyncHalibut syncOrAsyncHalibut,
            Version? version,
            RpcCallStage rpcCallStage
            ) : base(tentacleType, syncOrAsyncHalibut, version)
        {
            RpcCallStage = rpcCallStage;
        }
    }
}
