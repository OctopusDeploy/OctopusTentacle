using System;
using Autofac;
using NuGet;
using Octopus.Shared.BuiltInFeed;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Packages
{
    public class NuGetModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            HttpClient.DefaultCredentialProvider = FeedCredentialsProvider.Instance;

            builder.Register(c =>
            {
                MachineCache.Default.Clear();
                return new OctopusPackageRepositoryFactory(c.Resolve<ILog>())
                {
                    BuiltInRepositoryFactory = c.ResolveOptional<IBuiltInPackageRepositoryFactory>()
                };
            }).As<IPackageRepositoryFactory>();
        }
    }
}