using System;
using Autofac;

namespace Octopus.Shared.Integration.Azure
{
    public class AzureModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<AzurePackageUploader>().As<IAzurePackageUploader>();
        }
    }
}