using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Packages;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha;
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

            builder.RegisterType<ScriptServiceV3AlphaExecutorFactory>().As<IScriptServiceV3AlphaExecutorFactory>();
            builder.RegisterType<ShellScriptServiceV3AlphaExecutor>().AsSelf();
            builder.RegisterType<KubernetesJobScriptServiceV3AlphaExecutor>().AsSelf();
            //register the executor as the one resolved by the factory
            builder.Register(ctx =>
                {

                    var factory = ctx.Resolve<IScriptServiceV3AlphaExecutorFactory>();
                    return factory.GetExecutor();
                })
                .As<IScriptServiceV3AlphaExecutor>();

            // Register our Halibut services
            var knownServices = ThisAssembly.GetTypes()
                .Select(t => (ServiceImplementationType: t, ServiceAttribute: t.GetCustomAttribute<ServiceAttribute>()))
                .Where(x => x.ServiceAttribute != null)
                .Select(x => new KnownService(x.ServiceImplementationType, x.ServiceAttribute!.ServiceType))
                .ToArray();

            var assemblyServices = new KnownServiceSource(knownServices);
            builder.RegisterInstance(assemblyServices).AsImplementedInterfaces();
        }
    }
}