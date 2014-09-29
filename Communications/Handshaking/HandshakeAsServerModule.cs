using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Platform.Deployment.Configuration;
using Pipefish.Transport.SecureTcp;
using Pipefish.Transport.SecureTcp.Routing;
using Pipefish.Transport.SecureTcp.Server;
using Pipefish.Util;

namespace Octopus.Shared.Communications.Handshaking
{
    public class HandshakeAsServerModule : IRouteModule
    {
        readonly ICommunicationsConfiguration communicationsConfiguration;
        readonly IHandshakeReceiver handshakeReceiver;

        public HandshakeAsServerModule(ICommunicationsConfiguration communicationsConfiguration, IHandshakeReceiver handshakeReceiver)
        {
            if (communicationsConfiguration == null) throw new ArgumentNullException("communicationsConfiguration");
            if (handshakeReceiver == null) throw new ArgumentNullException("handshakeReceiver");
            this.communicationsConfiguration = communicationsConfiguration;
            this.handshakeReceiver = handshakeReceiver;
        }

        public void AddRoutes(Router router)
        {
            router.Add(Method.Post, "/handshake", Handshake, allowUnauthorized: true);
        }

        void Handshake(IncomingRequest request, IDictionary<string, string> parameters, OutgoingResponse response)
        {
            if (request.ClientThumbprint == null)
            {
                response.SendText("A client certificate must be provided for handshaking to take place", StatusCode.BadRequest);
                return;
            }

            var content = Json.Deserialize<HandshakeRequest>(new StreamReader(request.Content));
            handshakeReceiver.HandshakeReceived(request.ClientThumbprint, content.Squid);

            var responseContent = new HandshakeResponse { Squid = communicationsConfiguration.Squid, Hostname = Environment.MachineName };
            response.SendText(Json.Serialize(responseContent), contentType: "application/json");
        }
    }
}
