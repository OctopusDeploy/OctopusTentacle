using System;

namespace Octopus.Shared.Conventions.Implementations
{
    public class DeployFailedScriptConvention : ScriptConvention, IRollbackConvention
    {
        public override int Priority
        {
            get { return ConventionPriority.PreDeployScript; }
        }

        public override string FriendlyName
        {
            get { return "DeployFailed Script"; }
        }

        public void Rollback(ConventionContext context)
        {
            RunScript("DeployFailed", context);
        }

        public void Cleanup(ConventionContext context)
        {
            DeleteScript("DeployFailed", context);
        }
    }
}