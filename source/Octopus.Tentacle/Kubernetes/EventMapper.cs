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
            var source = EventHelpers.MetricSourceMapper(kEvent);
            if (kEvent.Reason.Equals(expectedReason))
            {
                var eventTimestamp = EventHelpers.GetLatestTimestampInEvent(kEvent)!.Value.ToUniversalTime();
                return new EventRecord(expectedReason, source, eventTimestamp);
            }
            return null;
        }
    }
    
    public class AgentKilledEventMapper : IEventMapper
    {
        public EventRecord? MapToRecordableEvent(Corev1Event kEvent)
        {
            const string expectedReason = "Killing"; 
            var source = EventHelpers.MetricSourceMapper(kEvent);
            if (kEvent.Reason.Equals(expectedReason) && source.Equals("tentacle"))
            {
                var eventTimestamp = EventHelpers.GetLatestTimestampInEvent(kEvent)!.Value.ToUniversalTime();
                return new EventRecord(expectedReason, source, eventTimestamp);
            }
            return null;
        }
    }

    public class NfsPodRestarted : IEventMapper
    {
        public EventRecord? MapToRecordableEvent(Corev1Event kEvent)
        {
            const string expectedReason = "Killing";
            var source = EventHelpers.MetricSourceMapper(kEvent);
            if (kEvent.Reason.Equals(expectedReason) && source.Equals("nfs"))
            {
                var eventTimestamp = EventHelpers.GetLatestTimestampInEvent(kEvent)!.Value.ToUniversalTime();
                return new EventRecord(kEvent.Reason, source, eventTimestamp);
            }
            return null;
        }
    }
}