using System;
using System.Text;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;

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
            
            builder.Append($"{TentacleType}, ");
            builder.Append($"{SyncOrAsyncHalibut}, ");
            
            string version = Version?.ToString() ?? "Latest";
            builder.Append($"{version}");

            if (StopPortForwarderAfterFirstCall.HasValue)
            {
                builder.Append($", {StopPortForwarderAfterFirstCall!.Value}");
            }

            if (RpcCall.HasValue)
            {
                builder.Append($", {RpcCall!.Value}");
            }

            if (RpcCallStage.HasValue)
            {
                builder.Append($", {RpcCallStage!.Value}");
            }

            if (ScriptIsolationLevel.HasValue)
            {
                builder.Append($", {ScriptIsolationLevel!.Value}");
            }

            if (ScriptsInParallelTestCase != null)
            {
                builder.Append($", {ScriptsInParallelTestCase!}");
            }

            return builder.ToString();
        }
    }

    public class ScriptsInParallelTestCase
    {
        public static ScriptsInParallelTestCase NoIsolationSameMutex => new(ScriptIsolationLevel.NoIsolation, "sameMutex", ScriptIsolationLevel.NoIsolation, "sameMutex", nameof(NoIsolationSameMutex));
        public static ScriptsInParallelTestCase FullIsolationDifferentMutex =>new(ScriptIsolationLevel.FullIsolation, "mutex", ScriptIsolationLevel.FullIsolation, "differentMutex", nameof(FullIsolationDifferentMutex));
        
        public readonly ScriptIsolationLevel LevelOfFirstScript;
        public readonly string MutexForFirstScript;
        public readonly ScriptIsolationLevel LevelOfSecondScript;
        public readonly string MutexForSecondScript;
        
        private readonly string stringValue;

        private ScriptsInParallelTestCase(
            ScriptIsolationLevel levelOfFirstScript,
            string mutexForFirstScript,
            ScriptIsolationLevel levelOfSecondScript,
            string mutexForSecondScript,
            string stringValue)
        {
            LevelOfFirstScript = levelOfFirstScript;
            MutexForFirstScript = mutexForFirstScript;
            LevelOfSecondScript = levelOfSecondScript;
            MutexForSecondScript = mutexForSecondScript;
            this.stringValue = stringValue;
        }

        public override string ToString()
        {
            return stringValue;
        }
    }
}
