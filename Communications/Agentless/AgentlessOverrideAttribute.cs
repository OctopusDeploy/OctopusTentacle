using System;
using Octopus.Client.Model;

namespace Octopus.Shared.Communications.Agentless
{
    [AttributeUsage(AttributeTargets.Class)]
    public class AgentlessOverrideAttribute : Attribute
    {
        readonly CommunicationStyle communicationStyle;

        public AgentlessOverrideAttribute(CommunicationStyle communicationStyle)
        {
            this.communicationStyle = communicationStyle;
        }

        public CommunicationStyle CommunicationStyle
        {
            get { return communicationStyle; }
        }
    }
}
