using System;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Util;

namespace Octopus.Shared.Conventions.Implementations
{
    public class DeletePackageFileConvention : IInstallationConvention 
    {
        readonly IOctopusFileSystem fileSystem;

        public DeletePackageFileConvention(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public int Priority
        {
            get { return ConventionPriority.DeletePackageFile; }
        }

        public string FriendlyName { get { return "Delete Package"; } }

        public void Install(IConventionContext context)
        {
            var packages = fileSystem.EnumerateFiles(context.PackageContentsDirectoryPath, "*.nupkg");

            foreach (var package in packages)
            {
                context.Log.Info("Deleting package: " + package);
                fileSystem.DeleteFile(package);
            }
        }
    }
}
