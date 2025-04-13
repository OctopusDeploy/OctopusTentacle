using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Core.Services;
using Octopus.Tentacle.Core.Services.Scripts;
using Octopus.Tentacle.Core.Services.Scripts.StateStore;
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
            var allTypes = ThisAssembly.GetTypes().Concat(typeof(ScriptServiceV2).Assembly.GetTypes());
            RegisterHalibutServices<ServiceAttribute>(builder, allTypes);

            //only register kubernetes services when
            if (KubernetesSupportDetection.IsRunningAsKubernetesAgent)
            {
                RegisterHalibutServices<KubernetesServiceAttribute>(builder, allTypes);
            }
        }

        static void RegisterHalibutServices<T>(ContainerBuilder builder, IEnumerable<Type> allTypes) where T: Attribute, IServiceAttribute
        {
            var knownServices = allTypes
                .SelectMany(t => t.GetCustomAttributes<T>().Select(attr => (ServiceImplementationType: t, ServiceAttribute: attr)))
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