using System;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Conventions.Implementations
{
    public class AzureConfigurationConvention : IInstallationConvention
    {
        public int Priority { get { return ConventionPriority.AzureConfiguration; } }
        public string FriendlyName { get { return "Azure Configuration"; } }

        public void Install(ConventionContext context)
        {
            if (!context.Variables.GetFlag(SpecialVariables.Step.IsAzureDeployment, false))
                return;

            context.Log.Info("We should update the cscfg here");
        }
    }
}