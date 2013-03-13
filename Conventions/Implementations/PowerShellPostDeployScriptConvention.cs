using System;

namespace Octopus.Shared.Conventions
{
    public class PowerShellPostDeployScriptConvention : PowerShellConvention, IInstallationConvention
    {
        public override int Priority
        {
            get { return ConventionPriority.PostDeployScript; }
        }

        public override string FriendlyName
        {
            get { return "PostDeploy.ps1"; }
        }

        public void Install(ConventionContext context)
        {
            RunScript("PostDeploy.ps1", context);
            DeleteScript("PostDeploy.ps1", context);
        }
    }
}