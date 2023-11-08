using System;
using System.Collections.Generic;
using Autofac;
using Halibut;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Variables;

namespace Octopus.Tentacle.Communications
{
    public class TentacleCommunicationsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<ProxyConfigParser>().As<IProxyConfigParser>();
            builder.RegisterType<HalibutInitializer>().As<IHalibutInitializer>();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();

            builder.Register(c =>
            {
                var configuration = c.Resolve<ITentacleConfiguration>();
                var services = c.Resolve<IServiceFactory>();

                bool.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleTcpKeepAliveEnabled), out var tcpKeepAliveEnabled);
                var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimits
                {
                    TcpKeepAliveEnabled = tcpKeepAliveEnabled
                };

                var halibutRuntime = new HalibutRuntimeBuilder()
                    .WithServiceFactory(services)
                    .WithServerCertificate(configuration.TentacleCertificate)
                    .WithMessageSerializer(serializerBuilder => serializerBuilder.WithLegacyContractSupport())
                    .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                    .Build();

                halibutRuntime.SetFriendlyHtmlPageContent(FriendlyHtmlPageContent);
                halibutRuntime.SetFriendlyHtmlPageHeaders(FriendlyHtmlPageHeaders);
                return halibutRuntime;
            }).As<HalibutRuntime>().SingleInstance();
            builder.RegisterType<OctopusServerChecker>().As<IOctopusServerChecker>();
        }

        static readonly string FriendlyHtmlPageContent = @"
<!doctype html>
<html>
<head>
    <meta charset='utf8'>
    <meta http-equiv='x-ua-compatible' content='ie=edge'>
    <title>Octopus Tentacle</title>
    <style type='text/css'>
        body {
            font-family: 'Segoe UI', 'Open Sans', 'Helvetica Neue', Helvetica, Arial, sans-serif;
            font-size: 10pt;
        }

        h1 {
            font-size: 15pt;
            font-weight: 400;
            margin-top: 7pt;
            margin-bottom: 7pt;
        }
    </style>
</head>
<body>
    <h1>Octopus Tentacle configured successfully</h1>
    <p>If you can view this page, your Octopus Tentacle is configured and ready to accept deployment commands.</p>
    <p>This landing page is displayed when no X509 certificate is provided. Only Octopus Servers with a trusted certificate can control this Tentacle.</p>
</body>
</html>";

        static readonly IEnumerable<KeyValuePair<string, string>> FriendlyHtmlPageHeaders = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Content-Security-Policy", "default-src 'none'; style-src 'sha256-Og27Evh417GekW0LSWwdTR+KDPHniSjRY3CDgH5olCw='; img-src 'self'"),
            new KeyValuePair<string, string>("Referrer-Policy", "no-referrer"),
            new KeyValuePair<string, string>("X-Content-Type-Options", "nosniff"),
            new KeyValuePair<string, string>("X-Frame-Options", "DENY"),
            new KeyValuePair<string, string>("X-XSS-Protection", "1; mode=block"),
            new KeyValuePair<string, string>("Strict-Transport-Security", "max-age=31536000")
        };
    }
}