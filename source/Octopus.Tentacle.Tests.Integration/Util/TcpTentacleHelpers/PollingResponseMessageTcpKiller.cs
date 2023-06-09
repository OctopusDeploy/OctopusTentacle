using System;
using System.Threading;
using NSubstitute;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.TestPortForwarder;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    /// <summary>
    /// Will only kill the connection when data is being received from the Tentacle but before being sent to the client.
    ///
    /// Useful for when the aim is to kill a in-flight request.
    ///
    /// See ResponseMessageTcpKillerWorkarounds for ensuring the TCP connection is in a state this can kill
    /// </summary>
    public class PollingResponseMessageTcpKiller : IResponseMessageTcpKiller
    {
        private volatile bool killConnection = false;
        private volatile bool pauseConnection = false;
        private ILogger logger;
        private Action? pauseConnectionCallBack;

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

        public void PauseConnectionOnNextResponse(Action? callBack = null)
        {
            // Allow some time for control messages to be sent
            // We can't actually tell the difference between control messages being sent and a response message
            // so we have to just hope this is enough time.
            // Caller should verify that the request actually failed as expected.
            Thread.Sleep(TimeSpan.FromSeconds(1));
            logger.Information("Will pause connection next time tentacle sends data.");
            pauseConnection = true;
            pauseConnectionCallBack = callBack;
        }

        public IDataTransferObserver DataTransferObserver()
        {
            return new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, dataFromTentacle) =>
            {
                var size = dataFromTentacle.Length;
                //logger.Information($"Received: {size} from tentacle");
                if (pauseConnection)
                {
                    pauseConnection = false;
                    logger.Information("Pause connection");
                    tcpPump.Pause();
                    pauseConnectionCallBack?.Invoke();
                }

                if (killConnection)
                {
                    killConnection = false;
                    logger.Information("Killing connection");
                    tcpPump.Dispose();
                }
            }).Build();
        }
    }

    public static class ClientAndTentacleBuilderPollingResponseMessageTcpKillerExtensionMethods {
        public static ClientAndTentacleBuilder WithPollingResponseMessageTcpKiller(this ClientAndTentacleBuilder clientAndTentacleBuilder, out IResponseMessageTcpKiller pollingResponseMessageTcpKiller)
        {
            var myPollingResponseMessageTcpKiller = new PollingResponseMessageTcpKiller();
            pollingResponseMessageTcpKiller = myPollingResponseMessageTcpKiller;
            return clientAndTentacleBuilder.WithPortForwarder(
                    builder => builder.WithDataObserver(() => new BiDirectionalDataTransferObserverBuilder().ObserveDataClientToOrigin(myPollingResponseMessageTcpKiller.DataTransferObserver()).Build()));
        }
    }
}