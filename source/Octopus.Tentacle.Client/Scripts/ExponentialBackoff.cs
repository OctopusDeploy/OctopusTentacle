using System;

namespace Octopus.Tentacle.Client.Scripts
{
    internal class ExponentialBackoff
    {
        public const double SlowIncrease = 1.5;
        public const double FastIncrease = 2;

        readonly double min;
        readonly double max;
        readonly double @base;
        readonly bool includeJitter;
        readonly Random random = new();

        public ExponentialBackoff(TimeSpan min, TimeSpan max, double @base = SlowIncrease, bool includeJitter = false)
        {
            if (min <= TimeSpan.Zero)
            {
                throw new ArgumentException("The minimum TimeSpan must be greater than 0", nameof(min));
            }

            if (max <= min)
            {
                throw new ArgumentException("The maximum TimeSpan must be greater than the minimum TimeSpan", nameof(max));
            }

            if (@base <= 1)
            {
                throw new ArgumentException("The base must be greater than 1", nameof(@base));
            }

            this.min = min.TotalMilliseconds;
            this.max = max.TotalMilliseconds;
            this.@base = @base;
            this.includeJitter = includeJitter;
        }

        public TimeSpan Get(int n)
        {
            var duration = Math.Min(max, Math.Pow(@base, n) * min);
            if (includeJitter)
            {
                var jitter = random.NextDouble() * duration / 2;
                duration = duration / 2 + jitter;
            }

            duration = Math.Round(duration, MidpointRounding.ToEven);

            return TimeSpan.FromMilliseconds(duration);
        }
    }
}
