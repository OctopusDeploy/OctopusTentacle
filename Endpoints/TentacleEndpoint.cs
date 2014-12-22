using System;
using System.Collections.Generic;
using Octopus.Platform.Variables;

namespace Octopus.Platform.Model.Endpoints
{
    public abstract class TentacleEndpoint : Endpoint
    {
        protected TentacleEndpoint(IDictionary<string, Variable> raw)
            : base(raw)
        {
        }

        public string Thumbprint { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }

    }
}
