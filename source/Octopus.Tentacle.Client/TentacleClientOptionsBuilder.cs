using System;
using System.Collections.Generic;
using System.Linq;
using Halibut.Util;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client
{
    public class TentacleClientOptionsBuilder
    {
        AsyncHalibutFeature asyncHalibutFeature = AsyncHalibutFeature.Disabled;
        RpcRetrySettings rpcRetrySettings = new(false, TimeSpan.Zero);
        readonly HashSet<string> disabledScriptServices = new();

        public TentacleClientOptionsBuilder DisableScriptService(string serviceName)
        {
            if (serviceName.Equals(nameof(IScriptService)))
                throw new ArgumentException("IScriptService cannot be disabled as it's the minimum version available on all Tentacles.", nameof(serviceName));

            disabledScriptServices.Add(serviceName);
            return this;
        }

        internal TentacleClientOptionsBuilder SetAsyncHalibut(AsyncHalibutFeature asyncHalibut)
        {
            this.asyncHalibutFeature = asyncHalibut;
            return this;
        }
        internal TentacleClientOptionsBuilder SetRcRetrySettings(RpcRetrySettings settings)
        {
            rpcRetrySettings = settings;
            return this;
        }

        internal TentacleClientOptions Build() => new(disabledScriptServices.ToHashSet(), asyncHalibutFeature, rpcRetrySettings);
    }
}