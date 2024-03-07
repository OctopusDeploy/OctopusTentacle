using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public class TelemetryEventBatch
    {
        [JsonProperty("client_upload_time")]
        public long Timestamp { get; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        [JsonProperty("events")]
        public IEnumerable<TelemetryEvent> Events { get; }

        public TelemetryEventBatch(TelemetryEvent eventObj)
        {
            Events = new List<TelemetryEvent> { eventObj };
        }
    }
}