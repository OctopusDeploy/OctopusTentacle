using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Util;

namespace Octopus.Tentacle.Util
{
    public class OctopusPhysicalFileSystem : CorePhysicalFileSystem
    {
        public OctopusPhysicalFileSystem(ISystemLog log) : base(log, KubernetesSupportDetection.IsRunningAsKubernetesAgent)
        {
        }
    }
}