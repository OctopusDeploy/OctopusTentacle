using System;
using Halibut;
using Octopus.Client.Model;

namespace Octopus.Shared.Endpoints
{
    public abstract class TentacleEndpoint : Endpoint
    {
        readonly CommunicationStyle communicationStyle;

        protected TentacleEndpoint(CommunicationStyle communicationStyle)
        {
            this.communicationStyle = communicationStyle;
        }

        protected TentacleEndpoint(CommunicationStyle communicationStyle, Uri uri, string thumbprint) : this (communicationStyle)
        {
            Uri = uri;
            Thumbprint = thumbprint;
        }

        public override CommunicationStyle CommunicationStyle
        {
            get { return communicationStyle; }
        }

        public string Thumbprint { get; set; }
        public Uri Uri { get; set; }

        public ServiceEndPoint GetServiceEndPoint()
        {
            return new ServiceEndPoint(Uri, Thumbprint);
        }

        public override string ToString()
        {
            return Uri.ToString();
        }
    }
}
