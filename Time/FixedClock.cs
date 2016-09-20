using System;
using Octopus.Server.Extensibility.HostServices.Time;

namespace Octopus.Shared.Time
{
    public class FixedClock : IClock
    {
        DateTimeOffset now;

        public FixedClock(DateTimeOffset now)
        {
            this.now = now;
        }

        public void Set(DateTimeOffset value)
        {
            now = value;
        }

        public void WindForward(TimeSpan time)
        {
            now = now.Add(time);
        }

        public DateTimeOffset GetUtcTime()
        {
            return Clone().now;
        }

        public DateTimeOffset GetLocalTime()
        {
            return Clone().now.ToLocalTime();
        }

        FixedClock Clone()
        {
            return (FixedClock) this.MemberwiseClone();
        }
    }
}