using System;
using System.Diagnostics;

namespace Octopus.Shared.Time
{
    public class StopWatch : IStopWatch
    {
        readonly Stopwatch stopWatch = new Stopwatch();

        public double ElapsedTotalMinutes => stopWatch.Elapsed.TotalMinutes;

        public void Start()
        {
            stopWatch.Start();
        }

        public void Restart()
        {
            stopWatch.Restart();
        }
    }
}