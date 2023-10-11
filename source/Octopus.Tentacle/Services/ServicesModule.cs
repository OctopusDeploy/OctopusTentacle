using System;
using System.Linq;
using Autofac;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Packages;
using Octopus.Tentacle.Scripts;

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

            // Register the script executor logic
            builder.RegisterType<ScriptExecutorFactory>().As<IScriptExecutorFactory>();
            builder.RegisterType<ShellScriptExecutor>().AsSelf().As<IScriptExecutor>();

            // Register our Halibut services
            var serviceTypes = ThisAssembly.GetTypes().Where(t => t.GetCustomAttributes(typeof(ServiceAttribute), true).Length > 0).ToArray();
            var assemblyServices = new KnownServiceSource(serviceTypes);
            builder.RegisterInstance(assemblyServices).AsImplementedInterfaces();
        }
    }
}