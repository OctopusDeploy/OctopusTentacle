using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Kubernetes.Diagnostics
{
    public class MapFromConfigMapToEventList
    {
        public List<EventRecord> FromConfigMap(List<EventRecord> configMapContent)
        {
            return configMapContent;
        }

        public List<EventRecord> ToConfigMap(List<EventRecord> eventRecords)
        {
            return eventRecords;
        }
    }
}