using System;
using System.Threading;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    /// <summary>
    /// Will only kill the connection when data is being received from the Tentacle but before being sent to the client.
    ///
    /// Useful for when the aim is to kill a in-flight request. 
    /// </summary>
    public class PollingResponseMessageTcpKiller
    {
        private volatile bool killConnection = false;
        private ILogger logger;

        public PollingResponseMessageTcpKiller()
        {
            logger = new SerilogLoggerBuilder().Build().ForContext<PollingResponseMessageTcpKiller>();
        }

        public void KillConnectionOnNextResponse()
        {
            // Allow some time for control messages to be sent
            // We can't actually tell the difference between control messages being sent and a response message
            // so we have to just hope this is enough time.
            // Caller should verify that the request actually failed as expected.
            Thread.Sleep(TimeSpan.FromSeconds(1));
            logger.Information("Will kill connection next time tentacle sends data.");
            killConnection = true;
        }

        public IDataTransferObserver DataTransferObserver()
        {
            return new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, dataFromTentacle) =>
            {
                var size = dataFromTentacle.Length;
                //logger.Information($"Received: {size} from tentacle");
                if (killConnection)
                {
                    killConnection = false;
                    logger.Information("Killing connection");
                    tcpPump.Dispose();
                }
            }).Build();
        }
    }
    
    public static class PortForwarderBuilderPollingResponseMessageTcpKillerExtensionMethods {
        public static PortForwarderBuilder WithPollingResponseMessageTcpKiller(this PortForwarderBuilder portForwarderBuilder, out PollingResponseMessageTcpKiller pollingResponseMessageTcpKiller)
        {
            var myPollingResponseMessageTcpKiller = new PollingResponseMessageTcpKiller();
            pollingResponseMessageTcpKiller = myPollingResponseMessageTcpKiller;
            return portForwarderBuilder
                .WithDataObserver(() => new BiDirectionalDataTransferObserverBuilder().ObserveDataClientToOrigin(myPollingResponseMessageTcpKiller.DataTransferObserver()).Build());
        }
    }
}