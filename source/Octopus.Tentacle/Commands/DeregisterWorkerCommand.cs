using System;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Diagnostics;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Commands
{
    public class DeregisterWorkerCommand : AbstractStandardCommand
    {
        readonly Lazy<ITentacleConfiguration> configuration;
        readonly ISystemLog log;
        readonly IApplicationInstanceSelector selector;
        readonly ApiEndpointOptions api;
        bool allowMultiple;
        readonly IProxyConfigParser proxyConfig;
        readonly IOctopusClientInitializer octopusClientInitializer;
        readonly ISpaceRepositoryFactory spaceRepositoryFactory;
        string spaceName = null!;

        public const string DeregistrationSuccessMsg = "Worker deregistered successfully";
        public const string MultipleMatchErrorMsg = "The worker matches more than one machine on the server. To deregister all of these machines specify the --multiple flag.";

        public DeregisterWorkerCommand(Lazy<ITentacleConfiguration> configuration,
            ISystemLog log,
            IApplicationInstanceSelector selector,
            IProxyConfigParser proxyConfig,
            IOctopusClientInitializer octopusClientInitializer,
            ISpaceRepositoryFactory spaceRepositoryFactory,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(selector, log, logFileOnlyLogger)
        {
            this.configuration = configuration;
            this.log = log;
            this.selector = selector;
            this.proxyConfig = proxyConfig;
            this.octopusClientInitializer = octopusClientInitializer;
            this.spaceRepositoryFactory = spaceRepositoryFactory;

            api = AddOptionSet(new ApiEndpointOptions(Options));
            Options.Add("m|multiple", "Deregister all workers that use the same thumbprint", s => allowMultiple = true);
            Options.Add("space=", "The space which this worker will be deregistered from, - e.g. 'Finance Department' where Finance Department is the name of an existing space; the default value is the Default space, if one is designated.", s => spaceName = s);
        }

        protected override void Start()
        {
            base.Start();
            StartAsync().GetAwaiter().GetResult();
        }

        async Task StartAsync()
        {
            //if we are on a polling tentacle with a polling proxy set up, use the api through that proxy
            var proxyOverride = proxyConfig.ParseToWebProxy(configuration.Value.PollingProxyConfiguration);
            using (var client = await octopusClientInitializer.CreateClient(api, proxyOverride))
            {
                var spaceRepository = await spaceRepositoryFactory.CreateSpaceRepository(client, spaceName);
                await Deregister(spaceRepository);
            }
        }

        public async Task Deregister(IOctopusSpaceAsyncRepository repository)
        {
            if (configuration.Value.TentacleCertificate?.Thumbprint == null)
            {
                throw new ControlledFailureException("This Tentacle does not have a thumbprint. The Tentacle must have a thumbprint in order to deregister it.");
            }
            // 1. do the machine count/allowMultiple checks
            var matchingMachines = await repository.Workers.FindByThumbprint(configuration.Value.TentacleCertificate.Thumbprint);

            if (matchingMachines.Count == 0)
                throw new ControlledFailureException("No worker was found matching this Tentacle's thumbprint.");

            if (matchingMachines.Count > 1 && !allowMultiple)
                throw new ControlledFailureException(MultipleMatchErrorMsg);

            // 2. contact the server and de-register, this is independent to any tentacle configuration
            foreach (var machineResource in matchingMachines)
            {
                log.Info($"Deleting worker '{machineResource.Name}' from the Octopus Server...");
                await repository.Workers.Delete(machineResource);
            }

            var certificate = await repository.Certificates.GetOctopusCertificate();

            var instanceArg = selector.Current.InstanceName == null ? "" : $" --instance {selector.Current.InstanceName}";
            var executable = PlatformDetection.IsRunningOnWindows ? "Tentacle.exe" : "Tentacle";
            log.Info("The Octopus Server is still trusted. " +
                $"If you wish to remove trust for this Octopus Server, use '{executable} configure --remove-trust={certificate.Thumbprint}{instanceArg}'");

            log.Info(DeregistrationSuccessMsg);
        }
    }
}
