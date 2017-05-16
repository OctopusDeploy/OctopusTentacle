using System;
using Autofac;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Shared.Communications;
using Octopus.Shared.Configuration;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Communications
{
    public class TentacleCommunicationsModule : Module
    {
        public TentacleCommunicationsModule(ApplicationName applicationName)
        {
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<ProxyConfigParser>().As<IProxyConfigParser>();
            builder.RegisterType<HalibutInitializer>().As<IHalibutInitializer>();
            builder.RegisterType<AutofacServiceFactory>().As<IServiceFactory>();
            builder.Register(c =>
            {
                var configuration = c.Resolve<ITentacleConfiguration>();
                var services = c.Resolve<IServiceFactory>();
                var halibutRuntime = new HalibutRuntime(services, configuration.TentacleCertificate);
                halibutRuntime.SetFriendlyHtmlPageContent(FriendlyHtmlPageContent);
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
    }
}