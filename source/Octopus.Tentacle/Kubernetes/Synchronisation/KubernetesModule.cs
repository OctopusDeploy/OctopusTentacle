using System;
using Autofac;
using Octopus.Tentacle.Kubernetes.Synchronisation.Internal;

namespace Octopus.Tentacle.Kubernetes.Synchronisation
{
    public class SynchronisationModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterGeneric(typeof(SemaphoreSlimReleaserFactory<>)).As(typeof(ISemaphoreSlimReleaserFactory<>)).SingleInstance();
            builder.RegisterGeneric(typeof(ReferenceCountingKeyedLock<>)).As(typeof(IKeyedLock<>)).SingleInstance();
        }
    }
}