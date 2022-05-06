using System;

namespace Octopus.Shared.Time
{
    public interface IStopWatch
    {
        double ElapsedTotalMinutes { get; }
        void Start();
        void Restart();
    }
}