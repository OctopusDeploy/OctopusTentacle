using System;

namespace Octopus.Shared.Time
{
    public interface IStopWatch
    {
        void Start();
        void Restart();
        double ElapsedTotalMinutes { get; }
    }
}
