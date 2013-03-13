using System;

namespace Octopus.Shared.Conventions.Implementations
{
    public class PowerShellDeployFailedScriptConvention : PowerShellConvention, IRollbackConvention
    {
        public override int Priority
        {
            get { return ConventionPriority.PreDeployScript; }
        }

        public override string FriendlyName
        {
            get { return "DeployFailed.ps1"; }
        }

        public void Rollback(ConventionContext context)
        {
            RunScript("DeployFailed.ps1", context);
        }

        public void Cleanup(ConventionContext context)
        {
            DeleteScript("DeployFailed.ps1", context);
        }
    }
}