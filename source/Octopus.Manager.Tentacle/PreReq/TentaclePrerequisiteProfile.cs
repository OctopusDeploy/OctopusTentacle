using System;
using System.Collections.Generic;

namespace Octopus.Manager.Tentacle.PreReq
{
    public class TentaclePrerequisiteProfile : IPrerequisiteProfile
    {
        public TentaclePrerequisiteProfile()
        {
            Prerequisites = new IPrerequisite[]
            {
                new PowerShellPrerequisite()
            };
        }

        public IEnumerable<IPrerequisite> Prerequisites { get; }
    }
}