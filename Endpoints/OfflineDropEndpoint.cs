using System;
using System.Collections.Generic;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Endpoints
{
    public class OfflineDropEndpoint : AgentlessEndpoint
    {
        [Obsolete("Serialization constructor")]
        public OfflineDropEndpoint() : this(new Dictionary<string, Variable>()) { }

        public OfflineDropEndpoint(IDictionary<string, Variable> raw)
            : base(raw)
        {
        }

        public string DropFolderPath { get { return GetEndpointProperty<string>(); } set {SetEndpointProperty(value);} }

        public override string ToString()
        {
            return DropFolderPath ?? "(none)";
        }
    }
}
