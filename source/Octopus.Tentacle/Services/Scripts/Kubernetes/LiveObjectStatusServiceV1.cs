using System;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts.LiveObjectStatusServiceV1;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Services.Scripts.Kubernetes
{
    [KubernetesService(typeof(ILiveObjectStatusServiceV1))]
    public class LiveObjectStatusServiceV1 : ILiveObjectStatusServiceV1
    {
        readonly ISystemLog log;
      
        public LiveObjectStatusServiceV1(
            ISystemLog log)
        {
            this.log = log;
        }

        public void UpdateResources(string[] resources)
        {
            log.Info("Resources: " + string.Join(", ", resources));
            
            LobsterResources.UpdateNamespaces(resources);
        }
    }
}