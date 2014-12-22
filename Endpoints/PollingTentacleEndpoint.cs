using System;
using System.Collections.Generic;
using Octopus.Platform.Variables;

namespace Octopus.Platform.Model.Endpoints
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
