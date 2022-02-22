using System;
using Autofac;

namespace Octopus.Tentacle.Certificates
{
    public class CertificatesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<CertificateGenerator>().As<ICertificateGenerator>();
        }
    }
}