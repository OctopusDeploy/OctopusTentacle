using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Client.Model;
using Octopus.Client.Operations;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Commands
{
    public class RegisterMachineCommand : RegisterMachineCommand<IRegisterMachineOperation>
    {
        public RegisterMachineCommand(Lazy<IRegisterMachineOperation> lazyRegisterMachineOperation, Lazy<IWritableTentacleConfiguration> configuration, ISystemLog log, IApplicationInstanceSelector selector, Lazy<IOctopusServerChecker> octopusServerChecker, IProxyConfigParser proxyConfig, IOctopusClientInitializer octopusClientInitializer, ISpaceRepositoryFactory spaceRepositoryFactory, ILogFileOnlyLogger logFileOnlyLogger) : base(lazyRegisterMachineOperation, configuration, log, selector, octopusServerChecker, proxyConfig, octopusClientInitializer, spaceRepositoryFactory, logFileOnlyLogger)
        {
        }
    }

    public class RegisterMachineCommand<TRegisterMachineOperation> : RegisterMachineCommandBase<TRegisterMachineOperation> where TRegisterMachineOperation : IRegisterMachineOperation
    {
        readonly List<string> environments = new List<string>();
        readonly List<string> roles = new List<string>();
        readonly List<string> tenants = new List<string>();
        readonly List<string> tenantTgs = new List<string>();
        TenantedDeploymentMode tenantedDeploymentMode;

        public RegisterMachineCommand(Lazy<TRegisterMachineOperation> lazyRegisterMachineOperation,
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
            Options.Add("env|environment=", "The environment name, slug or Id to add the machine to - e.g., 'Production'; specify this argument multiple times to add multiple environments", s => environments.Add(s));
            Options.Add("r|role=", "The machine role that the machine will assume - e.g., 'web-server'; specify this argument multiple times to add multiple roles", s => roles.Add(s));
            Options.Add("tenant=", "A tenant who the machine will be connected to; specify this argument multiple times to add multiple tenants", s => tenants.Add(s));
            Options.Add("tenanttag=", "A tenant tag which the machine will be tagged with - e.g., 'CustomerType/VIP'; specify this argument multiple times to add multiple tenant tags", s => tenantTgs.Add(s));
            Options.Add("tenanted-deployment-participation=", $"How the machine should participate in tenanted deployments. Allowed values are {Enum.GetNames(typeof(TenantedDeploymentMode)).ReadableJoin()}.", s =>
            {
                if (Enum.TryParse<TenantedDeploymentMode>(s, ignoreCase: true, out var result))
                    tenantedDeploymentMode = result;
                else
                    throw new ControlledFailureException($"The value '{s}' is not valid. Valid values are {Enum.GetNames(typeof(TenantedDeploymentMode)).ReadableJoin()}.");
            });
        }

        protected override void CheckArgs()
        {
            if (environments.Count == 0 || string.IsNullOrWhiteSpace(environments.First()))
                throw new ControlledFailureException("Please specify an environment name, slug or Id, e.g., --environment=Development");

            if (roles.Count == 0 || string.IsNullOrWhiteSpace(roles.First()))
                throw new ControlledFailureException("Please specify a role name, e.g., --role=web-server");
        }

        protected override void EnhanceOperation(TRegisterMachineOperation registerOperation)
        {
            registerOperation.Tenants = tenants.ToArray();
            registerOperation.TenantTags = tenantTgs.ToArray();
            registerOperation.Environments = environments.ToArray();
            registerOperation.Roles = roles.ToArray();
            registerOperation.TenantedDeploymentParticipation = tenantedDeploymentMode;
        }
    }
}