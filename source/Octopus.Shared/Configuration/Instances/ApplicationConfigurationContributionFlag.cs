using System;

namespace Octopus.Shared.Configuration.Instances
{
    public static class ApplicationConfigurationContributionFlag
    {
        public const string ContributeSettingsFlag = "OCTOPUS_CONTRIBUTE_ENV_SETTINGS";
        
        public static bool CanContributeSettings => bool.TryParse(Environment.GetEnvironmentVariable(ContributeSettingsFlag), out var contribute) && contribute;
    }
}