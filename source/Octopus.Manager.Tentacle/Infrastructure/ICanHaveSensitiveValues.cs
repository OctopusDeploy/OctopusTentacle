using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public interface ICanHaveSensitiveValues
    {
        void ContributeSensitiveValues(ILog log);
    }
}