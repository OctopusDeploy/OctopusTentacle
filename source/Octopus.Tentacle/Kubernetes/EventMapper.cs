using System;
using System.Collections.Generic;
using System.Linq;
using k8s.Models;

namespace Octopus.Tentacle.Kubernetes
{
    public record EventRecord(string Reason, string Source, DateTimeOffset OccurredAt);
    
    public interface IEventMapper
    {
        EventRecord? MapToRecordableEvent(Corev1Event kEvent);
    }

    public class NfsStaleEventMapper : IEventMapper
    {
        public EventRecord? MapToRecordableEvent(Corev1Event kEvent)
        {
            const string expectedReason = "NfsWatchdogTimeout"; 
            if (kEvent.Reason.Equals(expectedReason))
            {
                var eventTimestamp = EventHelpers.GetLatestTimestampInEvent(kEvent)!.Value.ToUniversalTime();
                return new EventRecord(expectedReason, "nfs", eventTimestamp);
            }
            return null;
        }
    }
    
    public class AgentKilledEventMapper : IEventMapper
    {
        public EventRecord? MapToRecordableEvent(Corev1Event kEvent)
        {
            const string expectedReason = "Killing"; 
            if (kEvent.Reason.Equals(expectedReason) && kEvent.Name().StartsWith("octopus-agent-tentacle"))
            {
                var eventTimestamp = EventHelpers.GetLatestTimestampInEvent(kEvent)!.Value.ToUniversalTime();
                return new EventRecord(expectedReason, "tentacle", eventTimestamp);
            }
            return null;
        }
    }

    public class NfsPodRestarted : IEventMapper
    {
        public EventRecord? MapToRecordableEvent(Corev1Event kEvent)
        {
            var podLifecycleEventsOfInterest = new []{"Started", "Killing"};
            if (podLifecycleEventsOfInterest.Contains(kEvent.Reason) && kEvent.Name().StartsWith("octopus-agent-nfs"))
            {
                var eventTimestamp = EventHelpers.GetLatestTimestampInEvent(kEvent)!.Value.ToUniversalTime();
                return new EventRecord(kEvent.Reason, "tentacle", eventTimestamp);
            }
            return null;
        }
    }
}