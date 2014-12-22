using System;
using System.Collections.Generic;
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

        public override string ToString()
        {
            return "polling endpoint";
        }
    }
}
