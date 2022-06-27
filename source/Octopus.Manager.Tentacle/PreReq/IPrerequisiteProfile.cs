using System.Collections.Generic;

namespace Octopus.Manager.Tentacle.PreReq
{
    public interface IPrerequisiteProfile
    {
        IEnumerable<IPrerequisite> Prerequisites { get; }
    }
}