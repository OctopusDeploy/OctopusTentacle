using System;
using System.Linq;
using Autofac;
using Octopus.Shared.Packages;
using Octopus.Shared.Scripts;
using Octopus.Tentacle.Communications;

namespace Octopus.Tentacle.Services
{
    public class ServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<ScriptWorkspaceFactory>().As<IScriptWorkspaceFactory>();

            builder.RegisterType<NuGetPackageInstaller>().As<IPackageInstaller>();

            // Register our Halibut services
            var serviceTypes = ThisAssembly.GetTypes().Where(t => t.GetCustomAttributes(typeof(ServiceAttribute), true).Length > 0).ToArray();
            var assemblyServices = new KnownServiceSource(serviceTypes);
            builder.RegisterInstance(assemblyServices).AsImplementedInterfaces();
        }
    }
}