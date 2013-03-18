using System;
using System.IO;
using System.Linq;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Iis;
using Octopus.Shared.Util;

namespace Octopus.Shared.Conventions
{
    public class IisWebSiteConvention : IInstallationConvention 
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IInternetInformationServer iisServer;
        
        public IisWebSiteConvention(IOctopusFileSystem fileSystem, IInternetInformationServer iisServer)
        {
            this.fileSystem = fileSystem;
            this.iisServer = iisServer;
        }

        public int Priority
        {
            get { return ConventionPriority.IisWebSite; }
        }

        public string FriendlyName { get { return "IIS"; } }

        public void Install(ConventionContext context)
        {
            if (!context.Variables.GetFlag(SpecialVariables.Step.IsTentacleDeployment, false))
            {
                // This convention is only run when deploying to a Tentacle
                return;
            }

            if (context.Variables.GetFlag(SpecialVariables.Step.Package.LegacyNotAWebSite, false))
            {
                context.Log.Debug("The OctopusNotAWebSite variable has been set; skipping IIS configuration.");
                return;
            }

            var webRoot = GetRootMostDirectoryContainingWebConfig(context);
            if (webRoot == null)
            {
                context.Log.Debug("A web.config file was not found, so no IIS configuration will be performed.");
                return;
            }

            var iisSiteName = context.Variables.GetValue(SpecialVariables.Step.Package.LegacyWebSiteName) 
                ?? context.Package.PackageId;

            context.Log.InfoFormat("Updating IIS website named '{0}'", iisSiteName);

            var warnAsErrors = context.Variables.GetFlag(SpecialVariables.TreatWarningsAsErrors, false);
            var legacySupport = context.Variables.GetFlag(SpecialVariables.UseLegacyIisSupport, false);
            
            var updated = iisServer.OverwriteHomeDirectory(iisSiteName, webRoot, legacySupport);
            if (!updated)
            {
                var error = string.Format("Could not find an IIS website or virtual directory named '{0}' on the local machine. If you expected Octopus to update this for you, you should create the site and/or virtual directory manually. Otherwise you can ignore this message.", iisSiteName);
                if (warnAsErrors)
                {
                    throw new ArgumentException(error);
                }

                context.Log.Warn(error);
            }
            else
            {
                context.Log.InfoFormat("The IIS website named '{0}' has had its path updated to: '{1}'", iisSiteName, webRoot);                
            }
        }

        string GetRootMostDirectoryContainingWebConfig(ConventionContext context)
        {
            // Optimize for most common case.
            if (fileSystem.FileExists(Path.Combine(context.PackageContentsDirectoryPath, "Web.config")))
            {
                return context.PackageContentsDirectoryPath;
            }

            // Find all folders under package root and sort them by depth
            var dirs = fileSystem.EnumerateDirectoriesRecursively(context.PackageContentsDirectoryPath).ToList();
            return dirs.OrderBy(x => x.Count(c => c == '\\')).FirstOrDefault(dir => fileSystem.FileExists(Path.Combine(dir, "Web.config")));
        }
    }
}