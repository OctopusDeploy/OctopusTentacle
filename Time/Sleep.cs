using System;
using System.Threading;

namespace Octopus.Shared.Time
{
    public class Sleep : ISleep
    {
        public void For(int milliseconds)
        {
            Thread.Sleep(milliseconds);
        }
    }
}