using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;

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
            return portForwarderBuilder.WithDataObserver(new BiDirectionalDataTransferObserverBuilder()
                .ObserveDataClientToOrigin(new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, stream) => logger.Information("Tentacle sent {Count} bytes", stream.Length)).Build())
                .ObserveDataOriginToClient(new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, stream) => logger.Information("Client sent {Count} bytes", stream.Length)).Build())
                .Build);
        }
        
        private static PortForwarderBuilder WithDataLoggingForListening(this PortForwarderBuilder portForwarderBuilder)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<PortForwarder>();
            return portForwarderBuilder.WithDataObserver(new BiDirectionalDataTransferObserverBuilder()
                .ObserveDataOriginToClient(new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, stream) => logger.Information("Tentacle sent {Count} bytes", stream.Length)).Build())
                .ObserveDataClientToOrigin(new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, stream) => logger.Information("Client sent {Count} bytes", stream.Length)).Build())
                .Build);
        }
    }
}