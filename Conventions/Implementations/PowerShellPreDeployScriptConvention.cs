using System;

namespace Octopus.Shared.Conventions.Implementations
{
    public class PowerShellPreDeployScriptConvention : PowerShellConvention, IInstallationConvention
    {
        public override int Priority
        {
            get { return ConventionPriority.PreDeployScript; }
        }

        public override string FriendlyName
        {
            get { return "PreDeploy.ps1"; }
        }

        public void Install(ConventionContext context)
        {
            RunScript("PreDeploy.ps1", context);
            DeleteScript("PreDeploy.ps1", context);
        }
    }
}