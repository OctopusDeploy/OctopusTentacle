using Autofac;

namespace Octopus.Tentacle.Commands.OptionSets
{
    public class OctopusClientInitializerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<OctopusClientInitializer>().As<IOctopusClientInitializer>().SingleInstance();
        }
    }
}