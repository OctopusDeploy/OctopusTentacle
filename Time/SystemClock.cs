using System;

namespace Octopus.Shared.Time
{
    public class SystemClock : IClock
    {
        public DateTime GetUtcTime()
        {
            return DateTime.UtcNow;
        }
    }
}