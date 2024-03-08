using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public abstract class TelemetryEvent
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        
        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("device_id")]
        public string DeviceId { get; set;  }
        
        [JsonProperty("event_type")]
        public string EventType { get; }

        [JsonProperty("event_properties")]
        public IDictionary<string, string> EventProperties = new Dictionary<string, string>();

        protected TelemetryEvent(string eventType, string userId, string deviceId)
        {
            EventType = eventType;
            UserId = userId;
            DeviceId = deviceId;
        }
    }
}
