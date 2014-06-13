using System;
using Octopus.Platform.Model;
using Pipefish.Core;

namespace Octopus.Shared.Communications.Agentless
{
    public static class ProtocolExtensions
    {
        public const string ConnectionParametersHeaderName = "Connection-Parameters";
        public const string ForwardToTransientActorHeaderName = "Forward-To-Transient-Actor" + Pipefish.Core.ProtocolExtensions.NonForwardSuffix;
        public const string ForwardToTransientCommunicationStyleHeaderName = "Forward-To-Transient-Style" + Pipefish.Core.ProtocolExtensions.NonForwardSuffix;
        public const string ForwardToTransientConnectionParametersHeaderName = "Forward-To-Transient-Connection" + Pipefish.Core.ProtocolExtensions.NonForwardSuffix;

        public static void SetConnectionParameters(this Message message, string connectionParameters)
        {
            message.Headers.Add(ConnectionParametersHeaderName, connectionParameters);
        }

        public static bool TryGetConnectionParameters(this Message message, out string connectionParameters)
        {
            return message.Headers.TryGetValue(ConnectionParametersHeaderName, out connectionParameters);
        }

        public static void SetForwardToTransient(this Message message, ActorId actorId, CommunicationStyle communicationStyle, string connectionParameters)
        {
            message.Headers.Add(ForwardToTransientActorHeaderName, actorId.ToString());
            message.Headers.Add(ForwardToTransientCommunicationStyleHeaderName, communicationStyle.ToString());
            message.Headers.Add(ForwardToTransientConnectionParametersHeaderName, connectionParameters);
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

        public static bool TryGetForwardToTransientConnection(this Message message, out string connectionParameters)
        {
            return message.Headers.TryGetValue(ForwardToTransientActorHeaderName, out connectionParameters);
        }
    }
}
