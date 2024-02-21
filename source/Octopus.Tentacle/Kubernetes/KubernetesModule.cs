﻿using Autofac;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<KubernetesJobService>().As<IKubernetesJobService>().SingleInstance();
            builder.RegisterType<KubernetesClusterService>().As<IKubernetesClusterService>().SingleInstance();
            builder.RegisterType<KubernetesJobContainerResolver>().As<IKubernetesJobContainerResolver>().SingleInstance();
            builder.RegisterType<KubernetesConfigMapService>().As<IKubernetesConfigMapService>().SingleInstance();
            builder.RegisterType<KubernetesSecretService>().As<IKubernetesSecretService>().SingleInstance();
            builder.RegisterType<KubernetesPodService>().As<IKubernetesPodService>().SingleInstance();

            builder.RegisterType<KubernetesJobMonitorTask>().As<IKubernetesJobMonitorTask>().SingleInstance();
            builder.RegisterType<KubernetesJobMonitor>().As<IKubernetesJobMonitor>().As<IKubernetesJobStatusProvider>().SingleInstance();

#if DEBUG
            builder.RegisterType<LocalMachineKubernetesClientConfigProvider>().As<IKubernetesClientConfigProvider>().SingleInstance();
#else
            builder.RegisterType<InClusterKubernetesClientConfigProvider>().As<IKubernetesClientConfigProvider>().SingleInstance();
#endif
        }
    }
}