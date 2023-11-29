using Autofac;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<KubernetesJobService>().As<IKubernetesJobService>().SingleInstance();
            builder.RegisterType<KubernetesClusterService>().As<IKubernetesClusterService>().SingleInstance();
            builder.RegisterType<KubernetesJobContainerResolver>().As<IKubernetesJobContainerResolver>().SingleInstance();

#if DEBUG
            builder.RegisterType<LocalMachineKubernetesClientConfigProvider>().As<IKubernetesClientConfigProvider>().SingleInstance();
#else
            builder.RegisterType<InClusterKubernetesClientConfigProvider>().As<IKubernetesClientConfigProvider>().SingleInstance();
#endif
        }
    }
}