using System;
using System.Collections.Generic;
using System.Linq;
using k8s.Models;

namespace Octopus.Tentacle.Kubernetes
{
    public static class EventHelpers
    {
        public static DateTime? GetLatestTimestampInEvent(Corev1Event kEvent)
        {
            return new List<DateTime?>
                {
                    kEvent.EventTime,
                    kEvent.LastTimestamp,
                    kEvent.FirstTimestamp
                }.Where(dt => dt.HasValue)
                .OrderByDescending(dt => dt!.Value)
                .FirstOrDefault();
        }
        
    }
}