using System;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public class PortForwarderBuilder
    {
        private Uri originServer;

        public PortForwarderBuilder(Uri originServer)
        {
            this.originServer = originServer;
        }

        public static PortForwarderBuilder ForwardingToLocalPort(int localPort)
        {
            return new PortForwarderBuilder(new Uri("https://localhost:" + localPort));
        }

        public PortForwarder Build()
        {
            return new PortForwarder(originServer, TimeSpan.Zero);
        } 
    }
}