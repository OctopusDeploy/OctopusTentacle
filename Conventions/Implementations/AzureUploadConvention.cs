using System;
using System.IO;
using System.Linq;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Azure;
using Octopus.Shared.Util;

namespace Octopus.Shared.Conventions.Implementations
{
    public class AzureUploadConvention : IInstallationConvention
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IAzurePackageUploader azurePackageUploader;

        public AzureUploadConvention(IOctopusFileSystem fileSystem, IAzurePackageUploader azurePackageUploader)
        {
            this.fileSystem = fileSystem;
            this.azurePackageUploader = azurePackageUploader;
        }

        public int Priority { get { return ConventionPriority.AzureUpload; } }
        public string FriendlyName { get { return "Azure Upload"; } }

        public void Install(IConventionContext context)
        {
            if (!context.Variables.GetFlag(SpecialVariables.Step.IsAzureDeployment, false))
                return;

            var package = FindPackageToUpload(context);
            if (package == null)
                return;

            var uri = UploadPackage(context, package);

            context.Variables.Set(SpecialVariables.Step.Azure.UploadedPackageUri, uri.ToString());

            context.Log.Info("Package uploaded as: " + uri);
        }

        string FindPackageToUpload(IConventionContext context)
        {
            var packages = fileSystem.EnumerateFiles(context.PackageContentsDirectoryPath, "*.cspkg").ToList();
            if (packages.Count == 0)
            {
                // Try subdirectories
                packages = fileSystem.EnumerateFilesRecursively(context.PackageContentsDirectoryPath, "*.cspkg").ToList();
            }

            if (packages.Count == 0)
            {
                context.Log.Warn("Your package does not appear to contain any Azure Cloud Service package (.cspkg) files.");
                return null;
            }

            if (packages.Count > 1)
            {
                context.Log.Error("Your NuGet package contains more than one Cloud Service package (.cspkg) file, which is unsupported. Files: " + string.Concat(packages.Select(p => Environment.NewLine + " - " + p)));
                return null;
            }

            return packages.Single();
        }

        Uri UploadPackage(IConventionContext context, string packageFilePath)
        {
            context.Log.Info("Uploading package to Azure blob storage: " + packageFilePath);

            var packageVersion = context.Variables.Get(SpecialVariables.Step.Package.NuGetPackageVersion);
            var packageHash = Hash(packageFilePath);            
            var fileName = Path.ChangeExtension(Path.GetFileName(packageFilePath), "." + packageVersion + "_" + packageHash + ".cspkg");
            
            var subscription = SubscriptionDataFactory.CreateFromAzureStep(context.Variables, context.Certificate);
            return azurePackageUploader.Upload(subscription, packageFilePath, fileName, context.Log, context.CancellationToken);
        }

        string Hash(string packageFilePath)
        {
            using (var stream = fileSystem.OpenFile(packageFilePath, FileMode.Open))
            {
                return HashCalculator.Hash(stream);
            }
        }
    }
}