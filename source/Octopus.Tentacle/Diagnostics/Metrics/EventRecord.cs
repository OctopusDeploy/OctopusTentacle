using System;

namespace Octopus.Tentacle.Diagnostics.Metrics
{
    public record EventRecord(string Reason, string Source, DateTimeOffset Timestamp)
    {
        public override string ToString()
        {
            return $"{Timestamp}: Reason: {Reason}; Source: {Source}";
        }
    }
}