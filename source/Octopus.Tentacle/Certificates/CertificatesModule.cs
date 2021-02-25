using System;
using Autofac;
using Octopus.Shared.Security;

namespace Octopus.Tentacle.Certificates
{
    public class CertificatesModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<CertificateGenerator>().As<ICertificateGenerator>();
        }
    }
}