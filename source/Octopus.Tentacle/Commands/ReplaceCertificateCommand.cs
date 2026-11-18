using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class ReplaceCertificateCommand : AbstractStandardCommand
    {
        const int DefaultServerCommsPort = 10943;

        readonly Lazy<IWritableTentacleConfiguration> configuration;
        readonly IOctopusClientInitializer octopusClientInitializer;
        readonly ISpaceRepositoryFactory spaceRepositoryFactory;
        readonly ISystemLog log;
        readonly ApiEndpointOptions api;
        string name = null!;
        string id = null!;
        string spaceName = null!;
        string serverCommsAddress = null!;
        int? serverCommsPort;

        public ReplaceCertificateCommand(
            Lazy<IWritableTentacleConfiguration> configuration,
            IOctopusClientInitializer octopusClientInitializer,
            ISpaceRepositoryFactory spaceRepositoryFactory,
            ISystemLog log,
            IApplicationInstanceSelector selector,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(selector, log, logFileOnlyLogger)
        {
            this.configuration = configuration;
            this.octopusClientInitializer = octopusClientInitializer;
            this.spaceRepositoryFactory = spaceRepositoryFactory;
            this.log = log;

            api = AddOptionSet(new ApiEndpointOptions(Options));
            Options.Add("name=", "The name of the existing deployment target or worker in Octopus", s => name = s);
            Options.Add("id=", "The ID of the existing deployment target or worker in Octopus, e.g. Machines-1 or Workers-1", s => id = s);
            Options.Add("space=", "The name of the space the deployment target or worker is in. The default space will be used if omitted.", s => spaceName = s);
            Options.Add("server-comms-address=", "For a polling Tentacle, the comms address on the Octopus Server; the address of the Octopus Server will be used if omitted.", s => serverCommsAddress = s);
            Options.Add("server-comms-port=", "For a polling Tentacle, the comms port on the Octopus Server; the default is " + DefaultServerCommsPort + ". If specified, this will take precedence over any port number in server-comms-address.", s => serverCommsPort = int.Parse(s));
        }

        protected override void Start()
        {
            base.Start();
            StartAsync().GetAwaiter().GetResult();
        }

        async Task StartAsync()
        {
            if (string.IsNullOrWhiteSpace(name) == string.IsNullOrWhiteSpace(id))
                throw new ControlledFailureException("Please specify either --name or --id of the existing deployment target or worker.");

            using var client = await octopusClientInitializer.CreateClient(api, configuration.Value.ProxyConfiguration.UseDefaultProxy);
            var repository = await spaceRepositoryFactory.CreateSpaceRepository(client, spaceName);

            var machine = await FindMachine(repository);
            var server = new OctopusServerConfiguration((await client.ForSystem().CertificateConfiguration.GetOctopusCertificate()).Thumbprint);
            switch (machine.Endpoint)
            {
                case ListeningTentacleEndpointResource:
                    server.CommunicationStyle = CommunicationStyle.TentaclePassive;
                    break;
                case PollingTentacleEndpointResource polling:
                    server.CommunicationStyle = CommunicationStyle.TentacleActive;
                    server.Address = GetPollingServerAddress();
                    server.SubscriptionId = polling.Uri;
                    break;
                default:
                    throw new ControlledFailureException($"'{machine.Name}' uses a {machine.Endpoint.CommunicationStyle} endpoint. Only listening and polling Tentacles can have their certificate replaced.");
            }

            var certificate = configuration.Value.GenerateNewCertificate();
            log.Info($"Generated a new certificate with thumbprint {certificate.Thumbprint}");

            ((TentacleEndpointResource)machine.Endpoint).Thumbprint = certificate.Thumbprint;
            await Modify(repository, machine);
            log.Info($"Updated '{machine.Name}' ({machine.Id}) in Octopus to trust the new certificate");

            configuration.Value.AddOrUpdateTrustedOctopusServer(server);
            VoteForRestart();
        }

        async Task<MachineBasedResource> FindMachine(IOctopusSpaceAsyncRepository repository)
        {
            if (!string.IsNullOrWhiteSpace(id))
                return id.StartsWith("Workers-", StringComparison.OrdinalIgnoreCase)
                    ? await repository.Workers.Get(id, CancellationToken.None)
                    : await repository.Machines.Get(id, CancellationToken.None);

            var matches = new MachineBasedResource?[] { await repository.Machines.FindByName(name, CancellationToken.None), await repository.Workers.FindByName(name, CancellationToken.None) }
                .OfType<MachineBasedResource>()
                .ToArray();

            return matches.Length switch
            {
                0 => throw new ControlledFailureException($"No deployment target or worker named '{name}' was found."),
                1 => matches[0],
                _ => throw new ControlledFailureException($"Both a deployment target and a worker are named '{name}'. Please specify which one with --id.")
            };
        }

        static Task Modify(IOctopusSpaceAsyncRepository repository, MachineBasedResource machine)
            => machine is WorkerResource worker
                ? repository.Workers.Modify(worker, CancellationToken.None)
                : repository.Machines.Modify((MachineResource)machine, CancellationToken.None);

        Uri GetPollingServerAddress()
        {
            var address = string.IsNullOrEmpty(serverCommsAddress) ? api.ServerUri : new Uri(serverCommsAddress);
            var port = serverCommsPort ?? (string.IsNullOrEmpty(serverCommsAddress) ? DefaultServerCommsPort : address.Port);
            return new Uri($"https://{address.Host}:{port}");
        }
    }
}
