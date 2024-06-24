using System;
using Autofac;
using Octopus.Client;
using Octopus.Client.Operations;

namespace Octopus.Tentacle.Commands
{
    public class ClientModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterType<OctopusClientFactory>().AsSelf().AsImplementedInterfaces();
#pragma warning disable CS0618 // Type or member is obsolete
            builder.RegisterType<RegisterKubernetesClusterOperation>().AsSelf().AsImplementedInterfaces();
#pragma warning restore CS0618 // Type or member is obsolete
            builder.RegisterType<RegisterKubernetesWorkerOperation>().AsSelf().AsImplementedInterfaces();
            builder.RegisterType<RegisterKubernetesDeploymentTargetOperation>().AsSelf().AsImplementedInterfaces();
            builder.RegisterType<RegisterMachineOperation>().AsSelf().AsImplementedInterfaces();
            builder.RegisterType<RegisterWorkerOperation>().AsSelf().AsImplementedInterfaces();
            builder.RegisterType<SpaceRepositoryFactory>().As<ISpaceRepositoryFactory>();
        }
    }
}