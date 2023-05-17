using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    public static class PortForwarderDataSentLogger
    {
        public static PortForwarderBuilder WithDataLoggingForPolling(this PortForwarderBuilder portForwarderBuilder)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<PortForwarder>();
            return portForwarderBuilder.WithDataObserver(new BiDirectionalDataTransferObserverBuilder()
                .ObserveDataClientToOrigin(new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, stream) => logger.Information("Tentacle sent {Count} bytes", stream.Length)).Build())
                .ObserveDataOriginToClient(new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, stream) => logger.Information("Client sent {Count} bytes", stream.Length)).Build())
                .Build);
        }
    }
}