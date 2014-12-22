using System;
using Autofac;
using Octopus.Shared.Util;

namespace Octopus.Shared.Packages
{
    public class PackageStoreModule : Module
    {
        readonly Func<IComponentContext, string> rootDirectoryPath;

        public PackageStoreModule(Func<IComponentContext, string> rootDirectoryPath)
        {
            this.rootDirectoryPath = rootDirectoryPath;
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<LightweightPackageExtractor>()
                .As<IPackageExtractor>()
                .InstancePerDependency();

            builder.RegisterType<PackageDownloader>().As<IPackageDownloader>();

            builder.Register(c =>
            {
                var fileSystem = c.Resolve<IOctopusFileSystem>();
                var root = rootDirectoryPath(c);
                return new PackageStore(fileSystem, root);
            }).As<PackageStore>().As<IPackageStore>().InstancePerDependency();
        }
    }
}