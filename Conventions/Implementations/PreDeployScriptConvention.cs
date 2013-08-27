using System;
using Octopus.Platform.Deployment.Conventions;

namespace Octopus.Shared.Conventions.Implementations
{
    public class PreDeployScriptConvention : ScriptConvention, IInstallationConvention
    {
        public override int Priority
        {
            get { return ConventionPriority.PreDeployScript; }
        }

        public override string FriendlyName
        {
            get { return "PreDeploy Script"; }
        }

        public void Install(IConventionContext context)
        {
            RunScript("PreDeploy", context);
            DeleteScript("PreDeploy", context);
        }
    }
}