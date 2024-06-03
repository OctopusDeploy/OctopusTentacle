using System;

namespace Octopus.Tentacle.Kubernetes.Diagnostics
{
    public record EventRecord(string Reason, string Source, DateTimeOffset Timestamp)
    {
        public override string ToString()
        {
            return $"{Timestamp}: Reason: {Reason}; Source: {Source}";
        }
    }
}