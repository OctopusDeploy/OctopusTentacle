using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Security;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class ReplaceCertificateCommand : AbstractStandardCommand
    {
        readonly Lazy<IWritableTentacleConfiguration> configuration;
        readonly Lazy<ICertificateGenerator> certificateGenerator;
        readonly Lazy<ITentacleConfigurationUpgrade> configurationUpgrade;
        readonly IOctopusClientInitializer octopusClientInitializer;
        readonly IProxyConfigParser proxyConfig;
        readonly ISystemLog log;
        readonly ApiEndpointOptions api;
        string applyCurrentCertToTentaclesWithThumbprint = null!;

        public ReplaceCertificateCommand(
            Lazy<IWritableTentacleConfiguration> configuration,
            Lazy<ICertificateGenerator> certificateGenerator,
            Lazy<ITentacleConfigurationUpgrade> configurationUpgrade,
            IOctopusClientInitializer octopusClientInitializer,
            IProxyConfigParser proxyConfig,
            ISystemLog log,
            IApplicationInstanceSelector selector,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(selector, log, logFileOnlyLogger)
        {
            this.configuration = configuration;
            this.certificateGenerator = certificateGenerator;
            this.configurationUpgrade = configurationUpgrade;
            this.octopusClientInitializer = octopusClientInitializer;
            this.proxyConfig = proxyConfig;
            this.log = log;

            api = AddOptionSet(new ApiEndpointOptions(Options));
            Options.Add("apply-current-cert-to-tentacles-with-thumbprint=", "Does not create a new certificate. Instead, updates every deployment target and worker in Octopus that has this thumbprint to trust this Tentacle's current certificate.", s => applyCurrentCertToTentaclesWithThumbprint = s);
        }

        protected override void Start()
        {
            base.Start();
            StartAsync().GetAwaiter().GetResult();
        }

        async Task StartAsync()
        {
            var config = configuration.Value;
            var keepCurrentCertificate = !string.IsNullOrWhiteSpace(applyCurrentCertToTentaclesWithThumbprint);
            var oldThumbprint = keepCurrentCertificate ? applyCurrentCertToTentaclesWithThumbprint : config.TentacleCertificateThumbprint;
            if (string.IsNullOrWhiteSpace(oldThumbprint) || string.IsNullOrWhiteSpace(config.TentacleCertificateThumbprint))
            {
                throw new ControlledFailureException("This Tentacle has no certificate thumbprint in its configuration, so its registrations in Octopus cannot be found.");
            }

            using var client = await octopusClientInitializer.CreateClient(api, proxyConfig.ParseToWebProxy(config.PollingProxyConfiguration));
            var registrations = await FindRegistrations(client, oldThumbprint!);
            if (registrations.Count == 0)
            {
                throw new ControlledFailureException($"No deployment target or worker with thumbprint {oldThumbprint} was found in any space this API key can access.");
            }

            if (!keepCurrentCertificate)
            {
                // Creates a fresh configuration encryption key if it was lost, so the new certificate can be saved.
                configurationUpgrade.Value.PrepareKey();
                var certificate = certificateGenerator.Value.GenerateNew(CertificateExpectations.TentacleCertificateFullName);
                config.ImportCertificate(certificate);
                log.Info($"Replaced certificate {oldThumbprint} with {certificate.Thumbprint}. If this does not complete, re-run this command with --apply-current-cert-to-tentacles-with-thumbprint {oldThumbprint}");
            }

            foreach (var (repository, machine) in registrations)
            {
                ((TentacleEndpointResource)machine.Endpoint).Thumbprint = config.TentacleCertificateThumbprint;
                await Modify(repository, machine);
                log.Info($"Updated '{machine.Name}' ({machine.Id}) in Octopus to trust the new certificate");
            }

            VoteForRestart();
        }

        static async Task<List<(IOctopusSpaceAsyncRepository Repository, MachineBasedResource Machine)>> FindRegistrations(IOctopusAsyncClient client, string thumbprint)
        {
            var users = client.ForSystem().Users;
            var spaces = await users.GetSpaces(await users.GetCurrent());

            var registrations = new List<(IOctopusSpaceAsyncRepository, MachineBasedResource)>();
            foreach (var space in spaces)
            {
                var repository = client.ForSpace(space);
                var machines = (await repository.Machines.FindByThumbprint(thumbprint)).Cast<MachineBasedResource>()
                    .Concat(await repository.Workers.FindByThumbprint(thumbprint))
                    .Where(m => m.Endpoint is ListeningTentacleEndpointResource or PollingTentacleEndpointResource);
                registrations.AddRange(machines.Select(m => (repository, m)));
            }

            return registrations;
        }

        static Task Modify(IOctopusSpaceAsyncRepository repository, MachineBasedResource machine)
            => machine is WorkerResource worker
                ? repository.Workers.Modify(worker, CancellationToken.None)
                : repository.Machines.Modify((MachineResource)machine, CancellationToken.None);
    }
}
