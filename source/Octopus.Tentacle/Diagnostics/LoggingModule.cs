#if FULL_FRAMEWORK
#else
#endif
using System;
using System.Linq;
using Autofac;
using Autofac.Core;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics.Metrics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Diagnostics
{
    public class LoggingModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Only add the NLogAppender if it isn't already - otherwise we get twice the logs for the price of one
            if (!Log.Appenders.Any(a => a is NLogAppender))
                Log.Appenders.Add(new NLogAppender());

            builder.RegisterType<SystemLog>().As<ISystemLog>().SingleInstance();
            builder.Register(c => new LogFileOnlyLogger()).As<ILogFileOnlyLogger>().InstancePerLifetimeScope();
            builder.RegisterType<PersistenceProvider>()
                .Named<IPersistenceProvider>("KubernetesAgentMetricsConfigMap")
                .WithParameter("configMapName", "kubernetes-agent-metrics");
            builder.RegisterType<KubernetesAgentMetrics>().AsSelf().SingleInstance()
                .WithParameter(
                    new ResolvedParameter(
                        (pi, _) => pi.Name == "persistenceProvider",
                        (_, ctx) => ctx.ResolveNamed<IPersistenceProvider>("KubernetesAgentMetricsConfigMap")));
            builder.RegisterType<MapFromConfigMapToEventList>().AsSelf();
        }
    }
}