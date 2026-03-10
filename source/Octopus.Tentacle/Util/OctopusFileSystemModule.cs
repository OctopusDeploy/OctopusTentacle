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
            if (KubernetesSupportDetection.IsRunningAsKubernetesAgent)
            {
                builder.RegisterType<KubernetesPhysicalFileSystem>().AsSelf().Named<IOctopusFileSystem>("fileSystem");
                builder.RegisterDecorator<IOctopusFileSystem>(
                    (c, inner) => new RetryingKubernetesPhysicalFileSystem(inner),
                    fromKey: "fileSystem");
            }
            else
            {
                builder.RegisterType<OctopusPhysicalFileSystem>().As<IOctopusFileSystem>();                
            }
        }
    }
}