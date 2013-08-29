using System;
using System.IO;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Util;
using Octopus.Platform.Variables;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Conventions.Implementations
{
    public class CopyPackageConvention : IInstallationConvention 
    {
        readonly IOctopusFileSystem fileSystem;

        public CopyPackageConvention(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public int Priority
        {
            get { return ConventionPriority.CopyPackage; }
        }

        public string FriendlyName { get { return "Copy"; } }

        public void Install(IConventionContext context)
        {
            if (!context.Variables.GetFlag(SpecialVariables.Action.IsTentacleDeployment, false))
            {
                // This convention is only run when deploying to a Tentacle
                return;
            }

            var targetDirectory = context.Variables.Get(SpecialVariables.Action.Package.CustomInstallationDirectory);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                context.Log.Verbose("The package has been installed to: " + context.PackageContentsDirectoryPath);
                context.Log.VerboseFormat("If you would like the package to be installed to an alternative location, please specify the variable '{0}'", SpecialVariables.Action.Package.CustomInstallationDirectory);

                context.Variables.Set(SpecialVariables.Action.Package.CustomInstallationDirectory, context.PackageContentsDirectoryPath);
                return;
            }

            targetDirectory = Path.GetFullPath(targetDirectory);

            bool purgeFirst = false;
            var purgeFirstText = context.Variables.Get(SpecialVariables.Action.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment);
            if (!string.IsNullOrWhiteSpace(purgeFirstText))
            {
                bool.TryParse(purgeFirstText, out purgeFirst);
            }

            try
            {
                if (purgeFirst)
                {
                    context.Log.InfoFormat("Purging the directory '{0}'", targetDirectory);
                    fileSystem.PurgeDirectory(targetDirectory, DeletionOptions.TryThreeTimes);
                }

                context.Log.InfoFormat("Extracting package contents to '{0}'", targetDirectory);
                fileSystem.CopyDirectory(context.PackageContentsDirectoryPath, targetDirectory);
                context.PackageContentsDirectoryPath = targetDirectory;
            }
            catch (Exception ex)
            {
                context.Log.Error(ex, string.Format("Unable to copy the package to the specified directory '{0}'. One or more files in the directory may be locked by another process. You could use a PreDeploy.ps1 script to stop any processes that may be locking the file. Error details follow.", targetDirectory));
                throw;
            }
        }
    }
}
