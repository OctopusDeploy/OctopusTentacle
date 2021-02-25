using System;
using System.Net;
using Autofac;
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Security;
using Octopus.Shared.Startup;
using Octopus.Shared.Time;
using Octopus.Shared.Util;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Properties;
using Octopus.Tentacle.Services;
using Octopus.Tentacle.Versioning;

namespace Octopus.Tentacle
{
    public class Program : OctopusProgram
    {
        public Program(string[] commandLineArguments) : base("Octopus Deploy: Tentacle",
            OctopusTentacle.Version.ToString(),
            OctopusTentacle.InformationalVersion,
            OctopusTentacle.EnvironmentInformation,
            commandLineArguments)
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls
                | SecurityProtocolType.Tls11
                | SecurityProtocolType.Tls12;
        }

        protected override ApplicationName ApplicationName => ApplicationName.Tentacle;

        static int Main(string[] args)
        {
            return new Program(args).Run();
        }

        protected override IContainer BuildContainer(StartUpInstanceRequest startUpInstanceRequest)
        {
            var builder = new ContainerBuilder();

            builder.RegisterModule(new ShellModule());
            builder.RegisterModule(new ConfigurationModule(startUpInstanceRequest));
            builder.RegisterModule(new TentacleConfigurationModule());
            builder.RegisterModule(new LogMaskingModule());
            builder.RegisterModule(new OctopusClientInitializerModule());
            builder.RegisterModule(new LoggingModule());
            builder.RegisterModule(new OctopusFileSystemModule());
            builder.RegisterModule(new CertificatesModule());
            builder.RegisterModule(new TimeModule());
            builder.RegisterModule(new ClientModule());
            builder.RegisterModule(new TentacleCommunicationsModule(ApplicationName));
            builder.RegisterModule(new ServicesModule());
            builder.RegisterModule(new VersioningModule(GetType().Assembly));

            builder.RegisterCommand<CreateInstanceCommand>("create-instance", "Registers a new instance of the Tentacle service");
            builder.RegisterCommand<DeleteInstanceCommand>("delete-instance", "Deletes an instance of the Tentacle service");
            builder.RegisterCommand<WatchdogCommand>("watchdog", "Configure a scheduled task to monitor the Tentacle service(s)")
                .WithParameter("applicationName", ApplicationName);
            builder.RegisterCommand<CheckServicesCommand>("checkservices", "Checks the Tentacle instances are running")
                .WithParameter("applicationName", ApplicationName);
            builder.RegisterCommand<RunAgentCommand>("agent", "Starts the Tentacle Agent in debug mode", "", "run");
            builder.RegisterCommand<ConfigureCommand>("configure", "Sets Tentacle settings such as the port number and thumbprints");
            builder.RegisterCommand<UpdateTrustCommand>("update-trust", "Replaces the trusted Octopus Server thumbprint of any matching polling or listening registrations with a new thumbprint to trust");
            builder.RegisterCommand<RegisterMachineCommand>("register-with", "Registers this machine as a deployment target with an Octopus Server");
            builder.RegisterCommand<RegisterWorkerCommand>("register-worker", "Registers this machine as a worker with an Octopus Server");
            builder.RegisterCommand<ExtractCommand>("extract", "Extracts a NuGet package");
            builder.RegisterCommand<DeregisterMachineCommand>("deregister-from", "Deregisters this deployment target from an Octopus Server");
            builder.RegisterCommand<DeregisterWorkerCommand>("deregister-worker", "Deregisters this worker from an Octopus Server");
            builder.RegisterCommand<NewCertificateCommand>("new-certificate", "Creates and installs a new certificate for this Tentacle");
            builder.RegisterCommand<ShowThumbprintCommand>("show-thumbprint", "Show the thumbprint of this Tentacle's certificate");
            builder.RegisterCommand<ServiceCommand>("service", "Start, stop, install and configure the Tentacle service")
                .WithParameter("applicationName", ApplicationName)
                .WithParameter("serviceDescription", "Octopus Deploy: Tentacle deployment agent")
                .WithParameter("assemblyContainingService", typeof(Program).Assembly);
            builder.RegisterCommand<ProxyConfigurationCommand>("proxy", "Configure the HTTP proxy used by Octopus");
            builder.RegisterCommand<PollingProxyConfigurationCommand>("polling-proxy", "Configure the HTTP proxy used by polling Tentacles to reach the Octopus Server");
            builder.RegisterCommand<ServerCommsCommand>("server-comms", "Configure how the Tentacle communicates with an Octopus Server");
            builder.RegisterCommand<ImportCertificateCommand>("import-certificate", "Replace the certificate that Tentacle uses to authenticate itself");
            builder.RegisterCommand<PollCommand>("poll-server", "Configures an Octopus Server that this Tentacle will poll");
            builder.RegisterCommand<ListInstancesCommand>("list-instances", "Lists all installed Tentacle instances");
            builder.RegisterCommand<VersionCommand>("version", "Show the Tentacle version information");
            builder.RegisterCommand<ShowConfigurationCommand>("show-configuration", "Outputs the Tentacle configuration");

            return builder.Build();
        }
    }
}
