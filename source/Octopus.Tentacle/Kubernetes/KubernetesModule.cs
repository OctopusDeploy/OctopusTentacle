using Autofac;
using Octopus.Tentacle.Kubernetes.Scripts;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<KubernetesPodService>().As<IKubernetesPodService>().SingleInstance();
            builder.RegisterType<KubernetesClusterService>().As<IKubernetesClusterService>().SingleInstance();
            builder.RegisterType<KubernetesPodContainerResolver>().As<IKubernetesPodContainerResolver>().SingleInstance();
            builder.RegisterType<KubernetesConfigMapService>().As<IKubernetesConfigMapService>().SingleInstance();
            builder.RegisterType<KubernetesSecretService>().As<IKubernetesSecretService>().SingleInstance();
            builder.RegisterType<RunningKubernetesPod>().InstancePerLifetimeScope();

            builder.RegisterType<KubernetesPodMonitorTask>().As<IKubernetesPodMonitorTask>().SingleInstance();
            builder.RegisterType<KubernetesPodMonitor>().As<IKubernetesPodMonitor>().As<IKubernetesPodStatusProvider>().SingleInstance();

#if DEBUG
            builder.RegisterType<LocalMachineKubernetesClientConfigProvider>().As<IKubernetesClientConfigProvider>().SingleInstance();
#else
            builder.RegisterType<InClusterKubernetesClientConfigProvider>().As<IKubernetesClientConfigProvider>().SingleInstance();
#endif
        }
    }
}