using System;
using Autofac;

namespace Octopus.Shared.Security
{
    public class CertificatesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<CertificateGenerator>().As<ICertificateGenerator>();
        }
    }
}