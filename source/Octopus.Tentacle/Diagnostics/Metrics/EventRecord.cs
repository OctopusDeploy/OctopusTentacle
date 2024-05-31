using System;

namespace Octopus.Tentacle.Diagnostics.Metrics
{
    public record EventRecord(string reason, string soruce, DateTimeOffset tiemstamp);
}