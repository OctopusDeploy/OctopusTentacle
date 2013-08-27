using System;
using Autofac;
using NuGet;
using Octopus.Platform.Diagnostics;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Orchestration.Logging;

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
                return new OctopusPackageRepositoryFactory(c.Resolve<ILog>());
            }).As<IPackageRepositoryFactory>();
        }
    }
}