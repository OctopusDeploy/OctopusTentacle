using System;
using Autofac;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Util
{
    public class OctopusFileSystemModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            if (PlatformDetection.Kubernetes.IsRunningAsKubernetesAgent)
            {
                builder.RegisterType<KubernetesPhysicalFileSystem>().AsSelf().As<IOctopusFileSystem>();
            }
            else
            {
                builder.RegisterType<OctopusPhysicalFileSystem>().As<IOctopusFileSystem>();                
            }
        }
    }
}