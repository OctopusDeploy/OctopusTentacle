using System;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public enum RpcCallStage
    {
        // Server is trying to connect to a Listening Tentacle or Polling Tentacle is trying to connect to Server / has not yet dequeued a pending request
        Connecting,
        // The RPC Request has been sent by Server for Listening / the pending request has been dequeued for polling by no response has been received
        InFlight
    }

    public enum RpcCall
    {
        FirstCall,
        RetryingCall
    }
}