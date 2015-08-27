using System;
using Autofac;

namespace Octopus.Shared.Packages
{
    public class PackageExtractionModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<LightweightPackageExtractor>()
                .As<IPackageExtractor>()
                .InstancePerDependency();
        }
    }
}