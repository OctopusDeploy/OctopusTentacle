using System;

namespace Octopus.Shared.Time
{
    public interface IClock
    {
        DateTimeOffset GetUtcTime();
        DateTimeOffset GetLocalTime();
    }
}
