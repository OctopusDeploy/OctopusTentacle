using System;
using Octopus.Time;

namespace Octopus.Shared.Time
{
    public class SystemClock : IClock
    {
        public DateTimeOffset GetUtcTime()
            => DateTimeOffset.UtcNow;

        public DateTimeOffset GetLocalTime()
            => DateTimeOffset.Now;
    }
}