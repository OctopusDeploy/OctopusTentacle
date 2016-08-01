using System;
using Octopus.Server.Extensibility.Time;

namespace Octopus.Shared.Time
{
    public class SystemClock : IClock
    {
        public DateTimeOffset GetUtcTime()
        {
            return DateTimeOffset.UtcNow;
        }

        public DateTimeOffset GetLocalTime()
        {
            return DateTimeOffset.Now;
        }
    }
}