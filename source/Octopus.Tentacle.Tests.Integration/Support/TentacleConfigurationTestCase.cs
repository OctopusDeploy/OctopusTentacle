using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleConfigurationTestCase
    {
        public TentacleType TentacleType { get; }
        public SyncOrAsyncHalibut SyncOrAsyncHalibut { get; }

        public Version? Version { get; }
        public bool? StopPortForwarderAfterFirstCall { get; }
        public RpcCall? RpcCall { get; }
        public RpcCallStage? RpcCallStage { get; }
        public ScriptIsolationLevel? ScriptIsolationLevel { get; }
        public ScriptsInParallelTestCase? ScriptsInParallelTestCase { get; }

        public TentacleConfigurationTestCase(
            TentacleType tentacleType,
            SyncOrAsyncHalibut syncOrAsyncHalibut,
            Version? version,
            bool? stopPortForwarderAfterFirstCall,
            RpcCall? rpcCall,
            RpcCallStage? rpcCallStage,
            ScriptIsolationLevel? scriptIsolationLevel,
            ScriptsInParallelTestCase? scriptsInParallelTestCase)
        {
            TentacleType = tentacleType;
            SyncOrAsyncHalibut = syncOrAsyncHalibut;
            Version = version;
            StopPortForwarderAfterFirstCall = stopPortForwarderAfterFirstCall;
            RpcCall = rpcCall;
            RpcCallStage = rpcCallStage;
            ScriptIsolationLevel = scriptIsolationLevel;
            ScriptsInParallelTestCase = scriptsInParallelTestCase;
        }
    }

    public class ScriptsInParallelTestCase
    {
        public ScriptIsolationLevel levelOfFirstScript;
        public string mutexForFirstScript;
        public ScriptIsolationLevel levelOfSecondScript;
        public string mutexForSecondScript;

        public ScriptsInParallelTestCase(ScriptIsolationLevel levelOfFirstScript, string mutexForFirstScript, ScriptIsolationLevel levelOfSecondScript, string mutexForSecondScript)
        {
            this.levelOfFirstScript = levelOfFirstScript;
            this.mutexForFirstScript = mutexForFirstScript;
            this.levelOfSecondScript = levelOfSecondScript;
            this.mutexForSecondScript = mutexForSecondScript;
        }
    }
}
