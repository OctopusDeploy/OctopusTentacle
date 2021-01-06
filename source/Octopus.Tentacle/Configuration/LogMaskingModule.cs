using System;
using System.Linq;
using Autofac;
using Octopus.Shared.Diagnostics;
using Octopus.Tentacle.Configuration.Proxy;

namespace Octopus.Tentacle.Configuration
{
    public class LogMaskingModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ProxyPasswordMaskValuesProvider>().As<IProxyPasswordMaskValuesProvider>();
            builder.Register(b =>
            {
                var proxyPassword = b.Resolve<ITentacleConfiguration>().ProxyConfiguration.CustomProxyPassword;
                var sensitiveValues = b.Resolve<IProxyPasswordMaskValuesProvider>().GetProxyPasswordMaskValues(proxyPassword).ToArray();

                //Wrap the log context in another class so we don't register it on the container (ILogContext isn't usually global)
                return new SensitiveValueMasker(new LogContext(null, sensitiveValues));
            }).As<ISensitiveValueMasker>();
        }
    }
}