using System;
using Autofac;

namespace Octopus.Shared.Integration.Ftp
{
    public class FtpModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<FtpSynchronizer>().As<IFtpSynchronizer>();
        }
    }
}