using System;

namespace Octopus.Shared.Time
{
    public class FixedClock : IClock
    {
        DateTime now;

        public FixedClock(DateTime now)
        {
            this.now = now;
        }

        public void Set(DateTime value)
        {
            now = value;
        }

        public void WindForward(TimeSpan time)
        {
            now = now.Add(time);
        }

        public DateTime GetUtcTime()
        {
            return now;
        }
    }
}