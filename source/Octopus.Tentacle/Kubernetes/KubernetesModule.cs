using System;
using Autofac;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Octopus.Tentacle.Background;
using Octopus.Tentacle.Kubernetes.Diagnostics;
using Octopus.Tentacle.Kubernetes.Synchronisation;
using Octopus.Tentacle.Kubernetes.Synchronisation.Internal;

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
            
            builder.RegisterGeneric(typeof(ReferenceCountingKeyedBinarySemaphore<>)).As(typeof(IKeyedSemaphore<>)).SingleInstance();

            const string kubernetesAgentMetricsPersistence = "kubernetes-agent-metrics-persistence";
            builder.RegisterType<PersistenceProvider>();
            builder.Register<PersistenceProvider>(ctx => ctx.Resolve<PersistenceProvider.Factory>().Invoke(ConfigMapNames.AgentMetrics))
                .Named<IPersistenceProvider>(kubernetesAgentMetricsPersistence);
            builder.RegisterType<KubernetesAgentMetrics>();
            builder.Register<KubernetesAgentMetrics>(ctx => 
                ctx.Resolve<KubernetesAgentMetrics.Factory>()
                    .Invoke(ctx.ResolveNamed<IPersistenceProvider>(kubernetesAgentMetricsPersistence), ConfigMapNames.AgentMetricsConfigMapKey))
                .As<IKubernetesAgentMetrics>();
            
            builder.RegisterType<KubernetesEventMonitor>();
            builder.Register<KubernetesEventMonitor>(ctx => ctx.Resolve<KubernetesEventMonitor.Factory>().Invoke(KubernetesConfig.Namespace))
                .As<IKubernetesEventMonitor>();
            builder.RegisterType<KubernetesEventMonitorTask>().As<IBackgroundTask>();
            
            builder.RegisterType<KubernetesEventService>().As<IKubernetesEventService>();

            builder.RegisterType<NfsStaleEventMapper>().As<IEventMapper>();
            builder.RegisterType<TentacleKilledEventMapper>().As<IEventMapper>();
            builder.RegisterType<NfsPodRestarted>().As<IEventMapper>();
            
                
#if DEBUG
            builder.RegisterType<LocalMachineKubernetesClientConfigProvider>().As<IKubernetesClientConfigProvider>().SingleInstance();
#else
            builder.RegisterType<InClusterKubernetesClientConfigProvider>().As<IKubernetesClientConfigProvider>().SingleInstance();
#endif
        }
    }
}