using System;
using System.Threading;
using Octopus.Time;

namespace Octopus.Tentacle.Time
{
    public class Sleep : ISleep
    {
        public void For(int milliseconds)
        {
            Thread.Sleep(milliseconds);
        }
    }
}