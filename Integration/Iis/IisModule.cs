using System;
using Autofac;

namespace Octopus.Shared.Integration.Iis
{
    public class IisModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<InternetInformationServer>().AsImplementedInterfaces();
        }
    }
}
