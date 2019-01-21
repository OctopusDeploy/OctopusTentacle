using System;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Client;
using Octopus.Shared;
using Octopus.Shared.Configuration;
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

        public const string ThumbprintNotFoundMsg = "The server you supplied did not match the thumbprint stored in the configuration for this tentacle.";
        public const string DeregistrationSuccessMsg = "Machine deregistered successfully";
        public const string MultipleMatchErrorMsg = "The Tentacle matches more than one machine on the server. To deregister all of these machines specify the --multiple flag.";
        
        public DeregisterMachineCommand(Lazy<ITentacleConfiguration> configuration, 
                                        ILog log,
                                        IApplicationInstanceSelector selector,
                                        IProxyConfigParser proxyConfig,
                                        IOctopusClientInitializer octopusClientInitializer)
            : base(selector)
        {
            this.configuration = configuration;
            this.log = log;
            this.proxyConfig = proxyConfig;
            this.octopusClientInitializer = octopusClientInitializer;

            api = AddOptionSet(new ApiEndpointOptions(Options));
            Options.Add("m|multiple", "Deregister all machines that use the same thumbprint", s => allowMultiple = true);
        }

        protected override void Start()
        {
            StartAsync().GetAwaiter().GetResult();
        }

        async Task StartAsync()
        {
            //if we are on a polling tentacle with a polling proxy set up, use the api through that proxy
            var proxyOverride = proxyConfig.ParseToWebProxy(configuration.Value.PollingProxyConfiguration);
            using (var client = await octopusClientInitializer.CreateClient(api, proxyOverride))
            {
                await Deregister(new OctopusAsyncRepository(client));
            }
        }

        public async Task Deregister(IOctopusAsyncRepository repository)
        {
            // 1. check: do the machine count/allowMultiple checks first to prevent partial trust removal
            var matchingMachines = await repository.Machines.FindByThumbprint(configuration.Value.TentacleCertificate.Thumbprint);

            if (matchingMachines.Count == 0)
                throw new ControlledFailureException("No machine was found on the server matching this Tentacle's thumbprint.");

            if (matchingMachines.Count > 1 && !allowMultiple)
                throw new ControlledFailureException(MultipleMatchErrorMsg);
            
            // 2. contact the server and de-register, this is independant to any tentacle configuration
            foreach (var machineResource in matchingMachines)
            {
                log.Info($"Deleting machine '{machineResource.Name}' from the Octopus Server...");
                await repository.Machines.Delete(machineResource);
            }

            // 3. remove the trust from the tentancle cconfiguration
            var serverThumbprint = (await repository.CertificateConfiguration.GetOctopusCertificate())?.Thumbprint;

            if (configuration.Value.TrustedOctopusThumbprints.Count(t => t.Equals(serverThumbprint, StringComparison.InvariantCultureIgnoreCase)) == 0)
            {
                log.Error(ThumbprintNotFoundMsg);
                return;
            }

            log.Info($"Deleting entry '{serverThumbprint}' in tentacle.config");
            configuration.Value.RemoveTrustedOctopusServersWithThumbprint(serverThumbprint);

            log.Info(DeregistrationSuccessMsg);
            VoteForRestart();
        }
    }
}