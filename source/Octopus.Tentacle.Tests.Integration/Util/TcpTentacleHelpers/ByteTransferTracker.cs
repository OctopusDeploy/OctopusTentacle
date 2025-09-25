using System;
using System.Threading.Tasks;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.TestPortForwarder;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers
{
    /// <summary>
    /// Tracks bytes transferred and calls a callback with the current byte counts.
    /// Can track bytes sent from client to tentacle, tentacle to client, or both directions combined.
    /// </summary>
    public class ByteTransferTracker
    {
        private readonly ILogger logger;
        private readonly Func<long, long, long, Task>? bytesTransferredCallback;
        
        private long clientToTentacleBytes = 0;
        private long tentacleToClientBytes = 0;

        /// <summary>
        /// Creates a new ByteTransferTracker
        /// </summary>
        /// <param name="bytesTransferredCallback">Async callback invoked with (clientToTentacleBytes, tentacleToClientBytes, totalBytes) whenever bytes are transferred</param>
        public ByteTransferTracker(Func<long, long, long, Task>? bytesTransferredCallback = null)
        {
            this.bytesTransferredCallback = bytesTransferredCallback;
            logger = new SerilogLoggerBuilder().Build().ForContext<ByteTransferTracker>();
            
            logger.Information("ByteTransferTracker created");
        }

        /// <summary>
        /// Gets the total bytes sent from client to tentacle
        /// </summary>
        public long ClientToTentacleBytes => clientToTentacleBytes;

        /// <summary>
        /// Gets the total bytes sent from tentacle to client
        /// </summary>
        public long TentacleToClientBytes => tentacleToClientBytes;

        /// <summary>
        /// Gets the total bytes transferred in both directions
        /// </summary>
        public long TotalBytes => clientToTentacleBytes + tentacleToClientBytes;

        /// <summary>
        /// Creates a data transfer observer for monitoring client-to-tentacle data flow
        /// </summary>
        public IDataTransferObserver CreateClientToTentacleObserver()
        {
            return new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, stream) =>
            {
                clientToTentacleBytes += stream.Length;
                
                logger.Verbose("Client sent {BytesSent} bytes. Total client-to-tentacle: {TotalClientToTentacle}", 
                    stream.Length, clientToTentacleBytes);

                InvokeCallback();
            }).Build();
        }

        /// <summary>
        /// Creates a data transfer observer for monitoring tentacle-to-client data flow
        /// </summary>
        public IDataTransferObserver CreateTentacleToClientObserver()
        {
            return new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, stream) =>
            {
                tentacleToClientBytes += stream.Length;
                
                logger.Verbose("Tentacle sent {BytesSent} bytes. Total tentacle-to-client: {TotalTentacleToClient}", 
                    stream.Length, tentacleToClientBytes);

                InvokeCallback();
            }).Build();
        }

        private void InvokeCallback()
        {
            // Fire and forget - we don't want to block data transfer on the callback
            _ = Task.Run(async () =>
            {
                try
                {
                    if (bytesTransferredCallback != null)
                    {
                        await bytesTransferredCallback(clientToTentacleBytes, tentacleToClientBytes, TotalBytes);
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Exception occurred in byte transfer callback");
                }
            });
        }
    }

    public static class ClientAndTentacleBuilderByteTransferTrackerExtensionMethods
    {
        /// <summary>
        /// Configures the port forwarder to track bytes transferred and invoke an async callback with the current counts
        /// </summary>
        /// <param name="clientAndTentacleBuilder">The builder to configure</param>
        /// <param name="bytesTransferredCallback">Async callback invoked with (clientToTentacleBytes, tentacleToClientBytes, totalBytes) whenever bytes are transferred</param>
        /// <returns>The configured builder</returns>
        public static ClientAndTentacleBuilder WithByteTransferTracker(
            this ClientAndTentacleBuilder clientAndTentacleBuilder,
            Func<long, long, long, Task>? bytesTransferredCallback)
        {
            var byteTracker = new ByteTransferTracker(bytesTransferredCallback);

            return clientAndTentacleBuilder.WithPortForwarder(builder =>
            {
                var observerBuilder = new BiDirectionalDataTransferObserverBuilder();

                if (clientAndTentacleBuilder.TentacleType == TentacleType.Listening)
                {
                    // For listening tentacles: 
                    // OriginToClient = Tentacle -> Client, ClientToOrigin = Client -> Tentacle
                    observerBuilder
                        .ObserveDataOriginToClient(byteTracker.CreateTentacleToClientObserver())
                        .ObserveDataClientToOrigin(byteTracker.CreateClientToTentacleObserver());
                }
                else
                {
                    // For polling tentacles:
                    // ClientToOrigin = Tentacle -> Client, OriginToClient = Client -> Tentacle
                    observerBuilder
                        .ObserveDataClientToOrigin(byteTracker.CreateTentacleToClientObserver())
                        .ObserveDataOriginToClient(byteTracker.CreateClientToTentacleObserver());
                }

                return builder.WithDataObserver(() => observerBuilder.Build());
            });
        }
    }

}
