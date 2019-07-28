using System;
using System.Linq;
using System.Net;
using Autofac;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;

namespace Octopus.Tentacle.Configuration
{
    public class LogMaskingModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(b =>
            {
                var sensitiveValues = GetProxyPasswordVariants(b.Resolve<ITentacleConfiguration>().ProxyConfiguration.CustomProxyPassword);
                return new LogContext(null, sensitiveValues);
            }).As<ILogContext>().SingleInstance();
        }
        
        static string[] GetProxyPasswordVariants(string proxyPassword)
        {
            if (string.IsNullOrEmpty(proxyPassword))
                return new string[] { };

            //Env:HTTP_PROXY will contain the URL encoded version of the password
            string urlEncodedProxyPassword = WebUtility.UrlEncode(proxyPassword);

            return new[]
            {
                proxyPassword, 
                urlEncodedProxyPassword
            }.Distinct().ToArray();
        }
    }
}