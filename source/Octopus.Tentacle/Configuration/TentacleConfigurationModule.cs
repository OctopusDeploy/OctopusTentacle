using System;
using Autofac;
using Octopus.Tentacle.Configuration.EnvironmentVariableMappings;

namespace Octopus.Tentacle.Configuration
{
    public class TentacleConfigurationModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<MapsTentacleEnvironmentValuesToConfigItems>().As<IMapEnvironmentValuesToConfigItems>();

            builder.RegisterType<TentacleConfiguration>().As<ITentacleConfiguration>().SingleInstance();
            builder.RegisterType<WritableTentacleConfiguration>().As<IWritableTentacleConfiguration>().SingleInstance();
            builder.RegisterType<PollingProxyConfiguration>().As<IPollingProxyConfiguration>();
            builder.RegisterType<WritablePollingProxyConfiguration>().As<IWritablePollingProxyConfiguration>();
        }
    }
}