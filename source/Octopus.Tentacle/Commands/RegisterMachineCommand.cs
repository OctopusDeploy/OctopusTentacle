using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Client.Model;
using Octopus.Client.Operations;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Commands
{
    public class RegisterMachineCommand : RegisterMachineCommandBase<IRegisterMachineOperation>
    {
        readonly List<string> environmentNames = new List<string>();
        readonly List<string> roles = new List<string>();
        readonly List<string> tenants = new List<string>();
        readonly List<string> tenantTgs = new List<string>();
        TenantedDeploymentMode tenantedDeploymentMode;

        public RegisterMachineCommand(Lazy<IRegisterMachineOperation> lazyRegisterMachineOperation,
                                      Lazy<IWritableTentacleConfiguration> configuration,
                                      ISystemLog log,
                                      IApplicationInstanceSelector selector,
                                      Lazy<IOctopusServerChecker> octopusServerChecker,
                                      IProxyConfigParser proxyConfig,
                                      IOctopusClientInitializer octopusClientInitializer,
                                      ISpaceRepositoryFactory spaceRepositoryFactory,
                                      ILogFileOnlyLogger logFileOnlyLogger)
            : base(lazyRegisterMachineOperation, configuration, log, selector, octopusServerChecker, proxyConfig, octopusClientInitializer, spaceRepositoryFactory, logFileOnlyLogger)
        {
            Options.Add("env|environment=", "The environment name to add the machine to - e.g., 'Production'; specify this argument multiple times to add multiple environments", s => environmentNames.Add(s));
            Options.Add("r|role=", "The machine role that the machine will assume - e.g., 'web-server'; specify this argument multiple times to add multiple roles", s => roles.Add(s));
            Options.Add("tenant=", "A tenant who the machine will be connected to; specify this argument multiple times to add multiple tenants", s => tenants.Add(s));
            Options.Add("tenanttag=", "A tenant tag which the machine will be tagged with - e.g., 'CustomerType/VIP'; specify this argument multiple times to add multiple tenant tags", s => tenantTgs.Add(s));
            Options.Add("tenanted-deployment-participation=", $"How the machine should participate in tenanted deployments. Allowed values are {Enum.GetNames(typeof(TenantedDeploymentMode)).ReadableJoin()}.", s =>
            {
                if (Enum.TryParse<TenantedDeploymentMode>(s, out var result))
                    tenantedDeploymentMode = result;
                else
                    throw new ControlledFailureException($"The value '{s}' is not valid. Valid values are {Enum.GetNames(typeof(TenantedDeploymentMode)).ReadableJoin()}.");
            });
        }

        protected override void CheckArgs()
        {
            if (environmentNames.Count == 0 || string.IsNullOrWhiteSpace(environmentNames.First()))
                throw new ControlledFailureException("Please specify an environment name, e.g., --environment=Development");

            if (roles.Count == 0 || string.IsNullOrWhiteSpace(roles.First()))
                throw new ControlledFailureException("Please specify an role name, e.g., --role=web-server");
        }

        protected override void EnhanceOperation(IRegisterMachineOperation registerOperation)
        {
            registerOperation.Tenants = tenants.ToArray();
            registerOperation.TenantTags = tenantTgs.ToArray();
            registerOperation.EnvironmentNames = environmentNames.ToArray();
            registerOperation.Roles = roles.ToArray();
            registerOperation.TenantedDeploymentParticipation = tenantedDeploymentMode;
        }
    }
}