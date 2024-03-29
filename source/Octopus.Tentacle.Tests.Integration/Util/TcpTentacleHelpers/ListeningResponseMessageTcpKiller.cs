using System;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.TestPortForwarder;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    /// <summary>
    /// Will only kill the connection when data is being received from the Tentacle and only when it looks big enough to not be a control message.
    ///
    /// Very dodgy, caller should ensure that an exception is actually thrown from the RPC call.
    /// See ResponseMessageTcpKillerWorkarounds for ensuring the TCP connection is in a state this can kill
    /// </summary>
    public class ListeningResponseMessageTcpKiller : IResponseMessageTcpKiller
    {
        private volatile bool killConnection = false;
        private volatile bool pauseConnection = false;
        private ILogger logger;
        private Action? pauseConnectionCallBack;

        public ListeningResponseMessageTcpKiller()
        {
            logger = new SerilogLoggerBuilder().Build().ForContext<ListeningResponseMessageTcpKiller>();
        }

        public void KillConnectionOnNextResponse()
        {
            logger.Information("Will kill connection next time tentacle sends more than 45 bytes (ie something that looks bigger than a control message)");
            killConnection = true;
        }

        public void PauseConnectionOnNextResponse(Action? callBack = null)
        {
            logger.Information("Will pause connection next time tentacle sends more than 45 bytes (ie something that looks bigger than a control message)");
            pauseConnection = true;
            pauseConnectionCallBack = callBack;
        }

        public IDataTransferObserver DataTransferObserver()
        {
            return new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, dataFromTentacle) =>
            {
                // It seems messages around 45 and below are control messages - except for
                // Windows Server 2012, where the max size seems to be ~85.
                // So anything bigger must be the interesting one.
                //
                // We increased this value from 45 -> 85 to account for Windows Server 2012,
                // and all the integration test builds seem happy with it.
                // May need to be adjusted in the future to account for different OSs,
                // runtimes, lunar cycles, and/or any other random heuristic.
                const int assumedControlMessageMaxSize = 85;
                
                var size = dataFromTentacle.Length;
                if (pauseConnection && size > assumedControlMessageMaxSize)
                {
                    pauseConnection = false;
                    logger.Information("Pause connection");
                    tcpPump.Pause();
                    pauseConnectionCallBack?.Invoke();
                }

                if (killConnection && size > assumedControlMessageMaxSize)
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
