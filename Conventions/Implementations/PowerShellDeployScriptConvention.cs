using System;

namespace Octopus.Shared.Conventions
{
    public class PowerShellDeployScriptConvention : PowerShellConvention, IInstallationConvention
    {
        public override int Priority
        {
            get { return ConventionPriority.DeployScript; }
        }

        public override string FriendlyName
        {
            get { return "Deploy.ps1"; }
        }

        public void Install(ConventionContext context)
        {
            RunScript("Deploy.ps1", context);
            DeleteScript("Deploy.ps1", context);
        }
    }
}