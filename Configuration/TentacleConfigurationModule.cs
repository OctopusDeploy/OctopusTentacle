using System;
using Autofac;
using Octopus.Platform.Deployment.Configuration;

namespace Octopus.Shared.Configuration
{
    public class TentacleConfigurationModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<TentacleConfiguration>().As<ITentacleConfiguration, IMasterKeyConfiguration>().SingleInstance();
        }
    }
}