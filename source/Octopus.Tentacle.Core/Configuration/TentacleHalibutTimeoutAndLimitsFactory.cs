using System;
using Halibut.Diagnostics;
using Octopus.Tentacle.Core.Util;

namespace Octopus.Tentacle.Core.Configuration
{
    public static class TentacleHalibutTimeoutAndLimitsFactory
    {
        public static HalibutTimeoutsAndLimits CreateHalibutTimeoutsAndLimits()
        {
            if (!bool.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleTcpKeepAliveEnabled), out var tcpKeepAliveEnabled))
            {
                // Default to enabled if the environment variable is not provided
                tcpKeepAliveEnabled = true;
            }

            if (!bool.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleEnableDataStreamLengthChecks), out var enableDataStreamLengthChecks))
            {
                // Default to false, since we want to first use the feature in Octopus where it is easier to toggle.
                enableDataStreamLengthChecks = false;
            }
                
            if (!bool.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleUseTcpNoDelay), out var useTcpNoDelay))
            {
                // Default to disabled
                useTcpNoDelay = false;
            }
                
            if (!bool.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleUseAsyncListener), out var useAsyncListener))
            {
                // Default to disabled
                useAsyncListener = false;
            }

            var halibutTimeoutsAndLimits = HalibutTimeoutsAndLimits.RecommendedValues();

            halibutTimeoutsAndLimits.ThrowOnDataStreamSizeMismatch = enableDataStreamLengthChecks;
            halibutTimeoutsAndLimits.TcpKeepAliveEnabled = tcpKeepAliveEnabled;
            halibutTimeoutsAndLimits.TcpNoDelay = useTcpNoDelay;
            halibutTimeoutsAndLimits.UseAsyncListener = useAsyncListener;
            return halibutTimeoutsAndLimits;
        }
    }
}