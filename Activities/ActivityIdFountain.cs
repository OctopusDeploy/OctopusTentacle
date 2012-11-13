using System;
using System.Threading;

namespace Octopus.Shared.Activities
{
    public class ActivityIdFountain
    {
        int next = 1;

        public int NextId()
        {
            return Interlocked.Increment(ref next);
        }
    }
}