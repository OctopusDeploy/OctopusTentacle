using System;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    public interface IResponseMessageTcpKiller
    {
        public void KillConnectionOnNextResponse();
    }
    
    public static class ClientAndTentacleBuilderResponseMessageTcpKillerExtensionMethods {
        public static ClientAndTentacleBuilder WithResponseMessageTcpKiller(this ClientAndTentacleBuilder clientAndTentacleBuilder, out IResponseMessageTcpKiller pollingResponseMessageTcpKiller)
        {
            if (clientAndTentacleBuilder.TentacleType == TentacleType.Listening)
            {
                return clientAndTentacleBuilder.WithListeningResponseMessageTcpKiller(out pollingResponseMessageTcpKiller);
            }

            if (clientAndTentacleBuilder.TentacleType == TentacleType.Polling)
            {
                return clientAndTentacleBuilder.WithPollingResponseMessageTcpKiller(out pollingResponseMessageTcpKiller);
            }

            throw new Exception("Unsupported type: " + clientAndTentacleBuilder.TentacleType);
        }
    }
}