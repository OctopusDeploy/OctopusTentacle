using System;
using Newtonsoft.Json;
using Octopus.Client.Model;

namespace Octopus.Shared.Endpoints
{
    public abstract class Endpoint
    {
        [JsonIgnore]
        public abstract CommunicationStyle CommunicationStyle { get; }
    }
}
