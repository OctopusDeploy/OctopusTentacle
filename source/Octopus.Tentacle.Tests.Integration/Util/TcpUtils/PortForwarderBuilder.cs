using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpUtils
{
    public class PortForwarderBuilder
    {
        readonly Uri originServer;
        TimeSpan sendDelay = TimeSpan.Zero;

        private readonly List<Func<BiDirectionalDataTransferObserver>> observerFactory = new();

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
            this.observerFactory.Add(observerFactory);
            return this;
        }

        public PortForwarder Build()
        {
            return new PortForwarder(originServer, sendDelay, () =>
            {
                var results = observerFactory.Select(factory => factory()).ToArray();
                return BiDirectionalDataTransferObserver.Combiner(results);
            });
        }
    }
}