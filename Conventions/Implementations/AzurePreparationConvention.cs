using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.WindowsAzure.Management.Model;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Azure;
using Octopus.Shared.Util;

namespace Octopus.Shared.Conventions.Implementations
{
    public class AzureConfigurationConvention : IInstallationConvention
    {
        public int Priority { get { return ConventionPriority.AzureConfiguration; } }
        public string FriendlyName { get { return "Azure Configuration"; } }

        public void Install(ConventionContext context)
        {
            if (!context.Variables.GetFlag(SpecialVariables.Step.IsAzureDeployment, false))
                return;

            context.Log.Info("We should update the cscfg here");
        }
    }

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

        public void Install(ConventionContext context)
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

        string FindPackageToUpload(ConventionContext context)
        {
            var packages = fileSystem.EnumerateFilesRecursively(context.PackageContentsDirectoryPath, "*.cspkg").ToList();
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

        Uri UploadPackage(ConventionContext context, string packageFilePath)
        {
            context.Log.Info("Uploading package to Azure blob storage: " + packageFilePath);

            return new Uri("https://paulstorage123.blob.core.windows.net/octopuspackages/Cloudy.Acme.CloudService.3.2.3_2a0fc371817a010db68fc14f8de55fbd1b7789c4.cspkg");

            //var packageId = context.Variables.GetValue(SpecialVariables.Step.Package.NuGetPackageId);
            //var packageVersion = context.Variables.GetValue(SpecialVariables.Step.Package.NuGetPackageVersion);
            //var packageHash = Hash(packageFilePath);
            //var fileName = Path.ChangeExtension(Path.GetFileName(packageFilePath), "." + packageId + "." + packageVersion + "_" + packageHash + ".cspkg");
            //var subscription = SubscriptionDataFactory.CreateFromAzureStep(context.Variables, context.Certificate);
            //return azurePackageUploader.Upload(subscription, packageFilePath, fileName, context.Log, context.CancellationToken);
        }

        string Hash(string packageFilePath)
        {
            using (var stream = fileSystem.OpenFile(packageFilePath, FileMode.Open))
            {
                return HashCalculator.Hash(stream);
            }
        }
    }

    public class AzureDeploymentConvention : PowerShellConvention, IInstallationConvention
    {
        readonly IOctopusFileSystem fileSystem;

        public AzureDeploymentConvention(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public override int Priority { get { return ConventionPriority.AzureDeployment; } }
        public override string FriendlyName { get { return "Azure Deployment"; } }

        public void Install(ConventionContext context)
        {
            if (!context.Variables.GetFlag(SpecialVariables.Step.IsAzureDeployment, false))
                return;

            var azurePowerShellFolder = Path.Combine(Path.GetDirectoryName(typeof (AzureDeploymentConvention).Assembly.FullLocalPath()), "Azure");

            var certificateFilePath = Path.Combine(context.PackageContentsDirectoryPath, "Certificate.pfx");
            var certificateFilePassword = Guid.NewGuid().ToString();
            var subscriptionName = Guid.NewGuid().ToString();
            
            try
            {
                CopyScriptFromTemplate(context, azurePowerShellFolder, "BootstrapDeployToAzure.ps1");
                CopyScriptFromTemplate(context, azurePowerShellFolder, "DeployToAzure.ps1");
                var configurationFilePath = ChooseWhichServiceConfigurationFileToUse(context);

                fileSystem.WriteAllBytes(certificateFilePath, context.Certificate.Export(X509ContentType.Pfx, certificateFilePassword));

                context.Variables.Set("OctopusAzureModulePath", Path.Combine(azurePowerShellFolder, "Azure.psd1"));
                context.Variables.Set("OctopusAzureCertificateFileName", certificateFilePath);
                context.Variables.Set("OctopusAzureCertificatePassword", certificateFilePassword);
                context.Variables.Set("OctopusAzureSubscriptionId", context.Variables.GetValue(SpecialVariables.Step.Azure.SubscriptionId));
                context.Variables.Set("OctopusAzureSubscriptionName", subscriptionName);
                context.Variables.Set("OctopusAzureServiceName", context.Variables.GetValue(SpecialVariables.Step.Azure.CloudServiceName));
                context.Variables.Set("OctopusAzureStorageAccountName", context.Variables.GetValue(SpecialVariables.Step.Azure.StorageAccountName));
                context.Variables.Set("OctopusAzureSlot", context.Variables.GetValue(SpecialVariables.Step.Azure.Slot));
                context.Variables.Set("OctopusAzurePackageUri", context.Variables.GetValue(SpecialVariables.Step.Azure.UploadedPackageUri));
                context.Variables.Set("OctopusAzureConfigurationFile", configurationFilePath);
                context.Variables.Set("OctopusAzureDeploymentLabel", context.Variables.GetValue(SpecialVariables.Step.Name) + " v" + context.Variables.GetValue(SpecialVariables.Release.Number));

                RunScript("BootstrapDeployToAzure.ps1", context);
            }
            finally
            {
                DeleteScript("DeployToAzure.ps1", context);
                DeleteScript("BootstrapDeployToAzure.ps1", context);
                fileSystem.DeleteFile(certificateFilePath, DeletionOptions.TryThreeTimes);
            }
        }

        string ChooseWhichServiceConfigurationFileToUse(ConventionContext context)
        {
            var configurationFilePath = Path.Combine(context.PackageContentsDirectoryPath, "ServiceConfiguration." + context.Variables.GetValue(SpecialVariables.Environment.Name) + ".cscfg");
            if (!fileSystem.FileExists(configurationFilePath))
            {
                configurationFilePath = Path.Combine(context.PackageContentsDirectoryPath, "ServiceConfiguration.Cloud.cscfg");
            }

            return configurationFilePath;
        }

        void CopyScriptFromTemplate(ConventionContext context, string azurePowerShellFolder, string fileName)
        {
            var sourceScriptFile = Path.Combine(azurePowerShellFolder, fileName);
            var destinationScriptFile = Path.Combine(context.PackageContentsDirectoryPath, fileName);
            if (!fileSystem.FileExists(destinationScriptFile))
            {
                fileSystem.CopyFile(sourceScriptFile, destinationScriptFile);
            }
        }
    }
}