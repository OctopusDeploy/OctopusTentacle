using System;
using Autofac;
using Autofac.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Octopus.Tentacle.Background;
using Octopus.Tentacle.Diagnostics.Metrics;

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

            builder.RegisterType<KubernetesScriptPodCreator>().As<IKubernetesScriptPodCreator>().SingleInstance();
            builder.RegisterType<KubernetesPodLogService>().As<IKubernetesPodLogService>().SingleInstance();
            builder.RegisterType<ScriptPodSinceTimeStore>().As<IScriptPodSinceTimeStore>().SingleInstance();
            builder.RegisterType<TentacleScriptLogProvider>().As<ITentacleScriptLogProvider>().SingleInstance();

            builder.RegisterType<KubernetesPodMonitorTask>().As<IKubernetesPodMonitorTask>().As<IBackgroundTask>().SingleInstance();
            builder.RegisterType<KubernetesPodMonitor>().As<IKubernetesPodMonitor>().As<IKubernetesPodStatusProvider>().SingleInstance();

            builder.RegisterType<KubernetesOrphanedPodCleanerTask>().As<IKubernetesOrphanedPodCleanerTask>().As<IBackgroundTask>().SingleInstance();
            builder.RegisterType<KubernetesOrphanedPodCleaner>().As<IKubernetesOrphanedPodCleaner>().SingleInstance();
            builder.RegisterType<KubernetesDirectoryInformationProvider>().As<IKubernetesDirectoryInformationProvider>().SingleInstance();
            builder.RegisterType<MemoryCache>().As<IMemoryCache>().SingleInstance();
            builder.RegisterType<MemoryCacheOptions>().As<IOptions<MemoryCacheOptions>>().SingleInstance();
            builder.RegisterType<PersistenceProvider>()
                .Named<IPersistenceProvider>("KubernetesAgentMetricsConfigMap")
                .WithParameter("configMapName", "kubernetes-agent-metrics");
            builder.RegisterType<KubernetesAgentMetrics>().AsSelf().SingleInstance()
                .WithParameter(
                    new ResolvedParameter(
                        (pi, _) => pi.Name == "persistenceProvider",
                        (_, ctx) => ctx.ResolveNamed<IPersistenceProvider>("KubernetesAgentMetricsConfigMap")))
                .WithParameter("entryName", "agent-metrics");
            builder.RegisterType<MapFromConfigMapToEventList>().AsSelf();
#if DEBUG
            builder.RegisterType<LocalMachineKubernetesClientConfigProvider>().As<IKubernetesClientConfigProvider>().SingleInstance();
#else
            builder.RegisterType<InClusterKubernetesClientConfigProvider>().As<IKubernetesClientConfigProvider>().SingleInstance();
#endif
        }
    }
}