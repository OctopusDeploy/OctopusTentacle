using System;
using Octopus.Client.Model;
using Octopus.Shared.Endpoints;
using Pipefish.Core;
using Pipefish.Util;

namespace Octopus.Shared.Communications.Agentless
{
    public static class ProtocolExtensions
    {
        public const string AgentlessEndpointHeaderName = "Agentless-Endpoint";
        public const string ForwardToTransientActorHeaderName = "Forward-To-Transient-Actor" + Pipefish.Core.ProtocolExtensions.NonForwardSuffix;
        public const string ForwardToTransientCommunicationStyleHeaderName = "Forward-To-Transient-Style" + Pipefish.Core.ProtocolExtensions.NonForwardSuffix;
        public const string ForwardToTransientEndpointHeaderName = "Forward-To-Transient-Endpoint" + Pipefish.Core.ProtocolExtensions.NonForwardSuffix;

        public static void SetAgentlessEndpoint(this Message message, string serializedEndpoint)
        {
            message.Headers.Add(AgentlessEndpointHeaderName, serializedEndpoint);
        }

        public static bool TryGetAgentlessEndpoint<TEndpoint>(this Message message, out TEndpoint endpoint)
            where TEndpoint : Endpoint
        {
            string serializedEndpoint;
            if (!message.Headers.TryGetValue(AgentlessEndpointHeaderName, out serializedEndpoint))
            {
                endpoint = null;
                return false;
            }

            endpoint = Json.Deserialize<TEndpoint>(serializedEndpoint);
            return true;
        }

        public static void SetForwardToTransient(this Message message, ActorId actorId, CommunicationStyle communicationStyle, Endpoint endpoint)
        {
            message.Headers.Add(ForwardToTransientActorHeaderName, actorId.ToString());
            message.Headers.Add(ForwardToTransientCommunicationStyleHeaderName, communicationStyle.ToString());
            message.Headers.Add(ForwardToTransientEndpointHeaderName, Json.Serialize(endpoint));
        }

        public static bool TryGetForwardToTransientActor(this Message message, out ActorId actorId)
        {
            string serialized;
            if (!message.Headers.TryGetValue(ForwardToTransientActorHeaderName, out serialized))
            {
                actorId = ActorId.Empty;
                return false;
            }
            
            actorId = new ActorId(serialized);
            return true;
        }

        public static bool TryGetForwardToTransientStyle(this Message message, out CommunicationStyle communicationStyle)
        {
            string serialized;
            if (!message.Headers.TryGetValue(ForwardToTransientCommunicationStyleHeaderName, out serialized))
            {
                communicationStyle = default(CommunicationStyle);
                return false;
            }

            communicationStyle = (CommunicationStyle)Enum.Parse(typeof(CommunicationStyle), serialized);
            return true;
        }

        public static bool TryGetForwardToTransientEndpoint(this Message message, out string serializedEndpoint)
        {
            return message.Headers.TryGetValue(ForwardToTransientEndpointHeaderName, out serializedEndpoint);
        }
    }
}
