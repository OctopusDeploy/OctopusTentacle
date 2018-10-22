﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Diagnostics;
using Octopus.Shared;
using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    public class DeregisterWorkerCommand : AbstractStandardCommand
    {
        readonly Lazy<ITentacleConfiguration> configuration;
        readonly ILog log;
        readonly ApiEndpointOptions api;
        bool allowMultiple;
        readonly IProxyConfigParser proxyConfig;
        readonly IOctopusClientInitializer octopusClientInitializer;
        string spaceName;

        public const string ThumbprintNotFoundMsg = "The server you supplied did not match the thumbprint stored in the configuration for this tentacle.";
        public const string DeregistrationSuccessMsg = "Machine deregistered successfully";
        public const string MultipleMatchErrorMsg = "The worker matches more than one machine on the server. To deregister all of these machines specify the --multiple flag.";

        public DeregisterWorkerCommand(Lazy<ITentacleConfiguration> configuration,
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
            Options.Add("m|multiple", "Deregister all workers that use the same thumbprint", s => allowMultiple = true);
            Options.Add("space=", "The space which this worker will be deregistered from, - e.g. 'Default' where Default is the name of an existing space; the default is the default space", s => spaceName = s);
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
                if (spaceName != null)
                {
                    var space = await client.Repository.Spaces.FindByName(spaceName);
                    using (var spaceClient = await client.ForSpace(space.Id))
                    {
                        await Deregister(new OctopusAsyncRepository(spaceClient));
                    }
                }
                else
                {
                    await Deregister(new OctopusAsyncRepository(client));
                }
            }
        }

        public async Task Deregister(IOctopusAsyncRepository repository)
        {
            // 1. check: do the machine count/allowMultiple checks first to prevent partial trust removal
            var matchingMachines = await repository.Workers.FindByThumbprint(configuration.Value.TentacleCertificate.Thumbprint);

            if (matchingMachines.Count == 0)
                throw new ControlledFailureException("No worker was found on the server matching this Tentacle's thumbprint.");

            if (matchingMachines.Count > 1 && !allowMultiple)
                throw new ControlledFailureException(MultipleMatchErrorMsg);

            // 2. contact the server and de-register, this is independant to any tentacle configuration
            foreach (var machineResource in matchingMachines)
            {
                log.Info($"Deleting worker '{machineResource.Name}' from the Octopus server...");
                await repository.Workers.Delete(machineResource);
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