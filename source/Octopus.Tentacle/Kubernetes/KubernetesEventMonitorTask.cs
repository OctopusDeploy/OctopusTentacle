using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Background;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesEventMonitorTask : BackgroundTask
    {
        
        public KubernetesEventMonitorTask(ISystemLog log, TimeSpan terminationGracePeriod) : base(log, terminationGracePeriod)
        {
        }
        
        protected override Task RunTask(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            throw new System.NotImplementedException();
        }

        public void Stop()
        {
            throw new System.NotImplementedException();
        }
    }
}