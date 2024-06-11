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

        public static string MetricSourceMapper(Corev1Event kEvent)
        {
            if (kEvent.Name().StartsWith("octopus-agent-tentacle"))
            {
                return "tentacle";
            }
            if (kEvent.Name().StartsWith(KubernetesScriptPodNameExtensions.OctopusScriptPodNamePrefix))
            {
                return "script";
            }
            
            if(kEvent.Name().StartsWith("octopus-agent-nfs"))
            {
                return "nfs";
            }

            return "unknown";
        }
        
    }
}