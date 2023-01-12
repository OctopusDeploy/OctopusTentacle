using System;

namespace Octopus.Manager.Tentacle.PreReq
{
    public interface IPrerequisite
    {
        string StatusMessage { get; }
        PrerequisiteCheckResult Check();
    }
}