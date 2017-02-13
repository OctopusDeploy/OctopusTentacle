using System;

namespace Octopus.Shared.Util
{
    public interface IMachineWideMutex
    {
        IDisposable Acquire(string name);
        IDisposable Acquire(string name, string waitMessage);
    }
}