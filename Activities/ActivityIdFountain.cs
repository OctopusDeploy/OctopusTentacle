using System;
using System.Threading;

namespace Octopus.Shared.Activities
{
    public class ActivityIdFountain
    {
        public string NextId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}