using System;
using Autofac;
using Octopus.Platform.Util;

namespace Octopus.Shared
{
    public class OctopusFileSystemModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterType<OctopusPhysicalFileSystem>().As<IOctopusFileSystem>();
        }
    }
}