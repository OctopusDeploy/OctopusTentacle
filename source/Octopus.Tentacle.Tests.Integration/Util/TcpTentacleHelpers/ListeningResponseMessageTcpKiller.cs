using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    /// <summary>
    /// Will only kill the connection when data is being received from the Tentacle and only when it looks big enough to not be a control message.
    ///
    /// Very dodgy, caller should ensure that an exception is actually thrown from the RPC call.
    /// </summary>
    public class ListeningResponseMessageTcpKiller : IResponseMessageTcpKiller
    {
        private volatile bool killConnection = false;
        private volatile bool pauseConnection = false;
        private ILogger logger;

        public ListeningResponseMessageTcpKiller()
        {
            logger = new SerilogLoggerBuilder().Build().ForContext<ListeningResponseMessageTcpKiller>();
        }

        public void KillConnectionOnNextResponse()
        {
            logger.Information("Will kill connection next time tentacle sends more than 45 bytes (ie something that looks bigger than a control message)");
            killConnection = true;
        }

        public void PauseConnectionOnNextResponse()
        {
            logger.Information("Will pause connection next time tentacle sends more than 45 bytes (ie something that looks bigger than a control message)");
            pauseConnection = true;
        }

        public IDataTransferObserver DataTransferObserver()
        {
            return new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, dataFromTentacle) =>
            {
                var size = dataFromTentacle.Length;
                // It seems messages around 45 and below are control messages
                // So anything bigger must be the interesting one.
                if (pauseConnection && size > 45)
                {
                    pauseConnection = false;
                    logger.Information("Pause connection");
                    tcpPump.Pause();
                }

                if (killConnection && size > 45)
                {
                    killConnection = false;
                    logger.Information("Killing connection");
                    tcpPump.Dispose();
                }
            }).Build();
        }
    }


    public static class ClientAndTentacleBuilderListeningResponseMessageTcpKillerExtensionMethods {
        public static ClientAndTentacleBuilder WithListeningResponseMessageTcpKiller(this ClientAndTentacleBuilder clientAndTentacleBuilder, out IResponseMessageTcpKiller pollingResponseMessageTcpKiller)
        {
            var myPollingResponseMessageTcpKiller = new ListeningResponseMessageTcpKiller();
            pollingResponseMessageTcpKiller = myPollingResponseMessageTcpKiller;
            // Client is octopus
            // Origin is tentacle
            return clientAndTentacleBuilder.WithPortForwarder(
                    builder => builder.WithDataObserver(() => new BiDirectionalDataTransferObserverBuilder()
                        .ObserveDataOriginToClient(myPollingResponseMessageTcpKiller.DataTransferObserver())
                        .Build()));
        }
    }
}