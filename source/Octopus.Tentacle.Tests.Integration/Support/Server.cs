using System;
using Halibut;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    internal class Server : IDisposable
    {
        public IHalibutRuntime ServerHalibutRuntime { get; }
        public int ServerListeningPort { get; }

        public Server(IHalibutRuntime serverHalibutRuntime, int serverListeningPort)
        {
            this.ServerHalibutRuntime = serverHalibutRuntime;
            ServerListeningPort = serverListeningPort;
        }

        public void Dispose()
        {
            ServerHalibutRuntime.Dispose();
        }
    }
}