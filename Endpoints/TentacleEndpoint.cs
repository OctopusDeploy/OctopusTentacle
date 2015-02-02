using System;
using System.Collections.Generic;
using Halibut;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Endpoints
{
    public abstract class TentacleEndpoint : Endpoint
    {
        protected TentacleEndpoint(IDictionary<string, Variable> raw)
            : base(raw)
        {
        }

        public string Thumbprint { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }

        public abstract ServiceEndPoint GetServiceEndPoint();
    }
}
