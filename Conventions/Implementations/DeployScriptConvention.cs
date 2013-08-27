using System;
using Octopus.Platform.Deployment.Conventions;

namespace Octopus.Shared.Conventions.Implementations
{
    public class DeployScriptConvention : ScriptConvention, IInstallationConvention
    {
        public override int Priority
        {
            get { return ConventionPriority.DeployScript; }
        }

        public override string FriendlyName
        {
            get { return "Deploy Script"; }
        }

        public void Install(IConventionContext context)
        {
            RunScript("Deploy", context);
            DeleteScript("Deploy", context);
        }
    }
}