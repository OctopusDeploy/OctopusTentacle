using System;
using System.IO;
using Octopus.Shared.Activities;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Conventions
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

        public void Install(ConventionContext context)
        {
            var targetDirectory = context.Variables.GetValue(SpecialVariables.PackageDirectoryPath);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                context.Log.Debug("The package has been installed to: " + context.PackageContentsDirectoryPath);
                context.Log.DebugFormat("If you would like the package to be installed to an alternative location, please specify the variable '{0}'", SpecialVariables.PackageDirectoryPath);
                
                context.Variables.Set(SpecialVariables.PackageDirectoryPath, context.PackageContentsDirectoryPath);
                return;
            }

            targetDirectory = Path.GetFullPath(targetDirectory);

            bool purgeFirst = false;
            var purgeFirstText = context.Variables.GetValue(SpecialVariables.PurgePackageDirectoryBeforeCopy);
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
                context.Log.Error(string.Format("Unable to copy the package to the specified directory '{0}'. One or more files in the directory may be locked by another process. You could use a PreDeploy.ps1 script to stop any processes that may be locking the file. Error details follow.", targetDirectory), ex);
                throw;
            }
        }
    }
}
