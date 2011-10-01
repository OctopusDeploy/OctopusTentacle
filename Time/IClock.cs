using System;

namespace Octopus.Shared.Time
{
    public interface IClock
    {
        DateTime GetUtcTime();
    }
}
