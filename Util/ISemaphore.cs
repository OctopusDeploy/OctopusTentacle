using System;

namespace Octopus.Shared.Util
{
    public interface ISemaphore
    {
        IDisposable Acquire(string name, string waitMessage);
    }
}
