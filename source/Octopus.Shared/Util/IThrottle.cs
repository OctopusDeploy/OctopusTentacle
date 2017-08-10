using System;
using System.Threading;

namespace Octopus.Shared.Util
{
    public interface IThrottle
    {
        IDisposable Wait(CancellationToken cancellationToken);
    }
}