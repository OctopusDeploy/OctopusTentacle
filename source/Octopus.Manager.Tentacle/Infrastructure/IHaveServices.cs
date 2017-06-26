using System.Collections.Generic;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public interface IHaveServices
    {
        IEnumerable<OctoService> Services { get; }
    }
}