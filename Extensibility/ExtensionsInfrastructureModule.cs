using Autofac;

namespace Octopus.Shared.Extensibility
{
    public class ExtensionsInfrastructureModule : Module
    {
        readonly ExtensionInfoProvider provider;

        public ExtensionsInfrastructureModule(ExtensionInfoProvider provider)
        {
            this.provider = provider;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => provider).As<IExtensionInfoProvider>().SingleInstance();
        }
    }
}