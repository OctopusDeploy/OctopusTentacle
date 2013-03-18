using System;

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

        public void Install(ConventionContext context)
        {
            RunScript("Deploy", context);
            DeleteScript("Deploy", context);
        }
    }
}