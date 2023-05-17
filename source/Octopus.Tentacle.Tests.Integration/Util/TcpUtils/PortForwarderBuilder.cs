using System;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpUtils
{
    public class PortForwarderBuilder
    {
        readonly Uri originServer;
        TimeSpan sendDelay = TimeSpan.Zero;

        Func<BiDirectionalDataTransferObserver> observerFactory = 
            () => new BiDirectionalDataTransferObserver(new DataTransferObserverBuilder().Build(), new DataTransferObserverBuilder().Build());

        public PortForwarderBuilder(Uri originServer)
        {
            this.originServer = originServer;
        }

        public static PortForwarderBuilder ForwardingToLocalPort(int localPort)
        {
            return new PortForwarderBuilder(new Uri("https://localhost:" + localPort));
        }

        public PortForwarderBuilder WithSendDelay(TimeSpan sendDelay)
        {
            this.sendDelay = sendDelay;
            return this;
        }

        public PortForwarderBuilder WithDataObserver(Func<BiDirectionalDataTransferObserver> observerFactory)
        {
            this.observerFactory = observerFactory;
            return this;
        }

        public PortForwarder Build()
        {
            return new PortForwarder(originServer, sendDelay, observerFactory);
        } 
    }
}
