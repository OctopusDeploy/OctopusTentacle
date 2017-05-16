using System;
using Autofac;
using Octopus.Shared.Packages;
using Octopus.Shared.Scripts;
using Octopus.Shared.Tasks;

namespace Octopus.Tentacle.Services
{
    public class ServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterAssemblyTypes(ThisAssembly)
                .AssignableTo<ITaskController>()
                .AsSelf()
                .InstancePerDependency();

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