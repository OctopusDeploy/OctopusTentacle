using System;
using Octopus.Diagnostics;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public interface ICanHaveSensitiveValues
    {
        void ContributeSensitiveValues(ILog log);
    }
}