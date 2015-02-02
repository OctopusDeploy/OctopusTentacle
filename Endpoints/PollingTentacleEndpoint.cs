using System;
using System.Collections.Generic;
using Halibut;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Endpoints
{
    /// <summary>
    /// Contains the information necessary to communicate with a polling tentacle.
    /// </summary>
    public class PollingTentacleEndpoint : TentacleEndpoint
    {
        public PollingTentacleEndpoint(IDictionary<string, Variable> raw)
            : base(raw)
        {
        }

        public Uri Uri { get { return GetEndpointProperty<Uri>(); } set { SetEndpointProperty(value); } }

        public override string ToString()
        {
            return "polling endpoint";
        }

        public override ServiceEndPoint GetServiceEndPoint()
        {
            return new ServiceEndPoint(Uri, Thumbprint);
        }
    }
}
