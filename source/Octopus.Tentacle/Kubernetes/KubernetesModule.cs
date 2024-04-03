using Autofac;
using Octopus.Tentacle.Background;
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

            // this needs to be per-dependency, otherwise it re-uses the RunningKubernetesPod
            builder.RegisterType<RunningKubernetesPod>().InstancePerDependency();
            builder.RegisterType<KubernetesPodScriptExecutor>().AsSelf();

            builder.RegisterType<KubernetesPodMonitorTask>().As<IKubernetesPodMonitorTask>().As<IBackgroundTask>().SingleInstance();
            builder.RegisterType<KubernetesPodMonitor>().As<IKubernetesPodMonitor>().As<IKubernetesPodStatusProvider>().SingleInstance();

            builder.RegisterType<KubernetesOrphanedPodCleanerTask>().As<IKubernetesOrphanedPodCleanerTask>().As<IBackgroundTask>().SingleInstance();
            builder.RegisterType<KubernetesOrphanedPodCleaner>().As<IKubernetesOrphanedPodCleaner>().SingleInstance();
#if DEBUG
            builder.RegisterType<LocalMachineKubernetesClientConfigProvider>().As<IKubernetesClientConfigProvider>().SingleInstance();
#else
            builder.RegisterType<InClusterKubernetesClientConfigProvider>().As<IKubernetesClientConfigProvider>().SingleInstance();
#endif
        }
    }
}