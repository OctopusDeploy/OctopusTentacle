using System;
using Autofac;
using Octopus.Shared.Packages;
using Octopus.Shared.Scripts;

namespace Octopus.Tentacle.Services
{
    public class ServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<ScriptWorkspaceFactory>().As<IScriptWorkspaceFactory>();

            builder.RegisterType<NuGetPackageInstaller>().As<IPackageInstaller>();

            // Find anything with the [Service] attribute
            builder.RegisterAssemblyTypes(ThisAssembly)
                .Where(t => t.GetCustomAttributes(typeof (ServiceAttribute), true).Length > 0)
                .Named(t => "I" + t.Name, typeof (object))
                .SingleInstance();
        }
    }
}