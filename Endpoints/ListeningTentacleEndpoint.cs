using System;
using System.Collections.Generic;
using Halibut;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Endpoints
{
    /// <summary>
    /// Contains the information necessary to communicate with a listening tentacle.
    /// </summary>
    public class ListeningTentacleEndpoint : TentacleEndpoint, IEndpointWithHostname
    {
        public ListeningTentacleEndpoint(IDictionary<string, Variable> raw)
            : base(raw)
        {
        }

        public Uri Uri { get { return GetEndpointProperty<Uri>(); } set { SetEndpointProperty(value); } }

        public override string ToString()
        {
            return Uri.ToString();
        }

        public string Host { get { return Uri.Host; } }

        public override ServiceEndPoint GetServiceEndPoint()
        {
            return new ServiceEndPoint(Uri, Thumbprint);
        }
    }
}