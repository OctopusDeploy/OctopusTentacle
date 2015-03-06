using System;
using Newtonsoft.Json;
using Octopus.Client.Model;

namespace Octopus.Shared.Endpoints
{
    public class ListeningTentacleEndpoint : TentacleEndpoint, IEndpointWithHostname
    {
        public ListeningTentacleEndpoint() : base(CommunicationStyle.TentaclePassive)
        {
        }

        public ListeningTentacleEndpoint(string uri, string thumbprint) : this(new Uri(uri), thumbprint)
        {
        }

        [JsonConstructor]
        public ListeningTentacleEndpoint(Uri uri, string thumbprint) : base(CommunicationStyle.TentaclePassive, uri, thumbprint)
        {
        }

        public string Host { get { return Uri.Host; } }
    }
}