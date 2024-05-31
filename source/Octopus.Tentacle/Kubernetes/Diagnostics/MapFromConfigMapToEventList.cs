using System.Collections.Generic;

namespace Octopus.Tentacle.Diagnostics.Metrics
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