using System;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.TestPortForwarder;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    public static class PortForwarderDataSentLogger
    {
        public static ClientAndTentacleBuilder WithPortForwarderDataLogging(this ClientAndTentacleBuilder clientAndTentacleBuilder)
        {
            if (clientAndTentacleBuilder.TentacleType == TentacleType.Listening) clientAndTentacleBuilder.WithPortForwarder(builder => builder.WithDataLoggingForListening());
            if (clientAndTentacleBuilder.TentacleType == TentacleType.Polling) clientAndTentacleBuilder.WithPortForwarder(builder => builder.WithDataLoggingForPolling());
            return clientAndTentacleBuilder;
        }

        private static PortForwarderBuilder WithDataLoggingForPolling(this PortForwarderBuilder portForwarderBuilder)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<PortForwarder>();
            return portForwarderBuilder.WithDataObserver(() => new BiDirectionalDataTransferObserverBuilder()
                .ObserveDataClientToOrigin(TentacleSent(logger))
                .ObserveDataOriginToClient(ClientSent(logger))
                .Build());
        }

        private static PortForwarderBuilder WithDataLoggingForListening(this PortForwarderBuilder portForwarderBuilder)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<PortForwarder>();
            return portForwarderBuilder.WithDataObserver(() => new BiDirectionalDataTransferObserverBuilder()
                .ObserveDataOriginToClient(TentacleSent(logger))
                .ObserveDataClientToOrigin(ClientSent(logger))
                .Build());
        }

        /// <summary>
        /// Client in this sense means the thing talking to Tentacle e.g. Octopus.
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static IDataTransferObserver ClientSent(ILogger logger)
        {
            long numberOfWrites = 0;
            long totalSent = 0;
            return new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, stream) =>
            {
                numberOfWrites++;
                totalSent += stream.Length;
                logger.Information("Client sent {Count} bytes. A total of {totalSent} bytes have been sent over {SentCount} distinct writes", 
                    stream.Length, totalSent, numberOfWrites);
            }).Build();
        }

        private static IDataTransferObserver TentacleSent(ILogger logger)
        {
            long numberOfWrites = 0;
            long totalSent = 0;
            return new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, stream) =>
            {
                numberOfWrites++;
                totalSent += stream.Length;
                logger.Information("Tentacle sent {Count} bytes. A total of {totalSent} bytes have been sent over {SentCount} distinct writes", 
                    stream.Length, totalSent, numberOfWrites);
            }).Build();
        }
    }
}