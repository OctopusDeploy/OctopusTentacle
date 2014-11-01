using System;

namespace Octopus.Shared.Communications.Handshaking
{
    public class HandshakeResponse
    {
        public string Squid { get; set; }
        public string Hostname { get; set; }
    }
}