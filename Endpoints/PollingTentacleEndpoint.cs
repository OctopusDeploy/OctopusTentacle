using System;
using Newtonsoft.Json;
using Octopus.Client.Model;

namespace Octopus.Shared.Endpoints
{
    public class PollingTentacleEndpoint : TentacleEndpoint
    {
        public PollingTentacleEndpoint() : base(CommunicationStyle.TentacleActive)
        {
        }

        [JsonConstructor]
        public PollingTentacleEndpoint(Uri uri, string thumbprint) : base(CommunicationStyle.TentacleActive, uri, thumbprint)
        {
        }
    }
}
