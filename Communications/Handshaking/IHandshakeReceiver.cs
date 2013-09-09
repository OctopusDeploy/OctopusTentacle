using System;

namespace Octopus.Shared.Communications.Handshaking
{
    public interface IHandshakeReceiver
    {
        void HandshakeReceived(string untrustedCallerThumbprint, string callerSquid); 
    }
}