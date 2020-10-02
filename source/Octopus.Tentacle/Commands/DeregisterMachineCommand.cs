using System;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Client;
using Octopus.Shared;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    public class DeregisterMachineCommand : AbstractStandardCommand
    {
        readonly Lazy<ITentacleConfiguration> configuration;
        readonly ILog log;
        readonly ApiEndpointOptions api;
        bool allowMultiple;
        readonly IProxyConfigParser proxyConfig;
        readonly IOctopusClientInitializer octopusClientInitializer;
        readonly ISpaceRepositoryFactory spaceRepositoryFactory;
        string spaceName;

        public const string DeregistrationSuccessMsg = "Machine deregistered successfully";
        public const string MultipleMatchErrorMsg = "The Tentacle matches more than one machine on the server. To deregister all of these machines specify the --multiple flag.";
        
        public DeregisterMachineCommand(Lazy<ITentacleConfiguration> configuration, 
                                        ILog log,
                                        IApplicationInstanceSelector selector,
                                        IProxyConfigParser proxyConfig,
                                        IOctopusClientInitializer octopusClientInitializer,
                                        ISpaceRepositoryFactory spaceRepositoryFactory)
            : base(selector)
        {
            this.configuration = configuration;
            this.log = log;
            this.proxyConfig = proxyConfig;
            this.octopusClientInitializer = octopusClientInitializer;
            this.spaceRepositoryFactory = spaceRepositoryFactory;

            api = AddOptionSet(new ApiEndpointOptions(Options));
            Options.Add("m|multiple", "Deregister all machines that use the same thumbprint", s => allowMultiple = true);
            Options.Add("space=", "The space which this machine will be deregistered from, - e.g. 'Finance Department' where Finance Department is the name of an existing space; the default value is the Default space, if one is designated.", s => spaceName = s);
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
            // 1. do the machine count/allowMultiple checks
            var matchingMachines = await repository.Machines.FindByThumbprint(configuration.Value.TentacleCertificate.Thumbprint);

            if (matchingMachines.Count == 0)
                throw new ControlledFailureException("No machine was found matching this Tentacle's thumbprint.");

            if (matchingMachines.Count > 1 && !allowMultiple)
                throw new ControlledFailureException(MultipleMatchErrorMsg);
            
            // 2. contact the server and de-register, this is independant to any tentacle configuration
            foreach (var machineResource in matchingMachines)
            {
                log.Info($"Deleting machine '{machineResource.Name}' from the Octopus Server...");
                await repository.Machines.Delete(machineResource);
            }

            log.Info("The Octopus Server is still trusted. " +
                "If you wish to remove trust for this Octopus Server, use 'Tentacle.exe configure --remove-trust=...'");

            log.Info(DeregistrationSuccessMsg);
        }
    }
}
