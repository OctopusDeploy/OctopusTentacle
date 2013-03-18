using System;

namespace Octopus.Shared.Conventions.Implementations
{
    public class PostDeployScriptConvention : ScriptConvention, IInstallationConvention
    {
        public override int Priority
        {
            get { return ConventionPriority.PostDeployScript; }
        }

        public override string FriendlyName
        {
            get { return "PostDeploy Script"; }
        }

        public void Install(ConventionContext context)
        {
            RunScript("PostDeploy", context);
            DeleteScript("PostDeploy", context);
        }
    }
}