using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Kubernetes.Scripts;
using Octopus.Tentacle.Packages;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;
using Module = Autofac.Module;

namespace Octopus.Tentacle.Services
{
    public class ServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<ScriptWorkspaceFactory>().As<IScriptWorkspaceFactory>();
            builder.RegisterType<ScriptStateStoreFactory>().As<IScriptStateStoreFactory>();

            builder.RegisterType<NuGetPackageInstaller>().As<IPackageInstaller>();

            // Register the script executor based on if we should
            if (PlatformDetection.Kubernetes.IsRunningAsKubernetesAgent)
            {
                builder.RegisterType<KubernetesPodScriptExecutor>().AsSelf().As<IScriptExecutor>();
            }
            else
            {
                builder.RegisterType<LocalShellScriptExecutor>().AsSelf().As<IScriptExecutor>();
            }

            // Register our Halibut services
            var knownServices = ThisAssembly.GetTypes()
                .Select(t => (ServiceImplementationType: t, ServiceAttribute: t.GetCustomAttribute<ServiceAttribute>()))
                .Where(x => x.ServiceAttribute != null)
                .Select(x => new KnownService(x.ServiceImplementationType, x.ServiceAttribute!.ContractType))
                .ToArray();

            var knownServiceSources = new KnownServiceSource(knownServices);
            builder.RegisterInstance(knownServiceSources).AsImplementedInterfaces();

            //register all halibut services with the root container
            foreach (var knownServiceSource in knownServices)
            {
                builder
                    .RegisterType(knownServiceSource.ServiceImplementationType)
                    .AsSelf()
                    .AsImplementedInterfaces()
                    .SingleInstance();
            }
        }
    }
}