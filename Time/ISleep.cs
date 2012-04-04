using System;

namespace Octopus.Shared.Time
{
    public interface ISleep
    {
        void For(int milliseconds);
    }
}