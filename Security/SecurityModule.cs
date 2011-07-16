using System;
using Autofac;

namespace Octopus.Shared.Security
{
    public class SecurityModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<CertificateStore>().As<ICertificateStore>();
        }
    }
}