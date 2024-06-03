using System;

namespace Octopus.Tentacle.Kubernetes.Diagnostics
{
    public record EventRecord(string Source, DateTimeOffset Timestamp)
    {
        public override string ToString()
        {
            return $"{Timestamp}:  Source: {Source}";
        }
    }
}