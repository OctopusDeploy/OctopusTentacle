using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Octopus.Tentacle.Communications;
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

            // Register our Halibut services
            var allTypes = ThisAssembly.GetTypes();
            RegisterHalibutServices<ServiceAttribute>(builder, allTypes);

            //only register kubernetes services when
            if (PlatformDetection.Kubernetes.IsRunningAsKubernetesAgent)
            {
                RegisterHalibutServices<KubernetesServiceAttribute>(builder, allTypes);
            }
        }

        static void RegisterHalibutServices<T>(ContainerBuilder builder, Type[] allTypes) where T: Attribute, IServiceAttribute
        {
            var knownServices = allTypes
                .Select(t => (ServiceImplementationType: t, ServiceAttribute: t.GetCustomAttribute<T>()))
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