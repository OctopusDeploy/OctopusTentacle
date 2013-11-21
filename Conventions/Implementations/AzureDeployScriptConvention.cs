using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Security.Certificates;
using Octopus.Platform.Util;
using Octopus.Platform.Variables;

namespace Octopus.Shared.Conventions.Implementations
{
    public class AzureDeployScriptConvention : ScriptConvention, IInstallationConvention
    {
        readonly IOctopusFileSystem fileSystem;

        public AzureDeployScriptConvention(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public override int Priority { get { return ConventionPriority.AzureDeployment; } }
        public override string FriendlyName { get { return "Azure Deployment"; } }

        public void Install(IConventionContext context)
        {
            if (!context.Variables.GetFlag(SpecialVariables.Action.IsAzureDeployment, false))
                return;

            var azurePowerShellFolder = Path.Combine(Path.GetDirectoryName(typeof (AzureDeployScriptConvention).Assembly.FullLocalPath()), "Azure");

            var certificateFilePath = Path.Combine(context.PackageContentsDirectoryPath, "Certificate.pfx");
            var certificateFilePassword = Guid.NewGuid().ToString();
            var subscriptionName = Guid.NewGuid().ToString();
            
            try
            {
                CopyScriptFromTemplate(context, azurePowerShellFolder, "BootstrapDeployToAzure.ps1");
                CopyScriptFromTemplate(context, azurePowerShellFolder, "DeployToAzure.ps1");

                var azureCertificate = CertificateEncoder.Import(
                    context.Variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint),
                    Convert.FromBase64String(context.Variables.Get(SpecialVariables.Action.Azure.CertificateBytes)));

                fileSystem.WriteAllBytes(certificateFilePath, azureCertificate.Export(X509ContentType.Pfx, certificateFilePassword));

                context.Variables.Set("OctopusAzureModulePath", Path.Combine(azurePowerShellFolder, "Azure.psd1"));
                context.Variables.Set("OctopusAzureCertificateFileName", certificateFilePath);
                context.Variables.Set("OctopusAzureCertificatePassword", certificateFilePassword);
                context.Variables.Set("OctopusAzureSubscriptionId", context.Variables.Get(SpecialVariables.Action.Azure.SubscriptionId));
                context.Variables.Set("OctopusAzureSubscriptionName", subscriptionName);
                context.Variables.Set("OctopusAzureServiceName", context.Variables.Get(SpecialVariables.Action.Azure.CloudServiceName));
                context.Variables.Set("OctopusAzureStorageAccountName", context.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName));
                context.Variables.Set("OctopusAzureSlot", context.Variables.Get(SpecialVariables.Action.Azure.Slot));
                context.Variables.Set("OctopusAzurePackageUri", context.Variables.Get(SpecialVariables.Action.Azure.UploadedPackageUri));
                context.Variables.Set("OctopusAzureDeploymentLabel", context.Variables.Get(SpecialVariables.Action.Name) + " v" + context.Variables.Get(SpecialVariables.Release.Number));
                context.Variables.Set("OctopusAzureSwapIfPossible", context.Variables.Get(SpecialVariables.Action.Azure.SwapIfPossible));
                context.Variables.Set("OctopusAzureUseCurrentInstanceCount", context.Variables.Get(SpecialVariables.Action.Azure.UseCurrentInstanceCount));

                RunScript("BootstrapDeployToAzure", context);
            }
            finally
            {
                DeleteScript("DeployToAzure", context);
                DeleteScript("BootstrapDeployToAzure", context);
                fileSystem.DeleteFile(certificateFilePath, DeletionOptions.TryThreeTimes);
            }
        }

        void CopyScriptFromTemplate(IConventionContext context, string azurePowerShellFolder, string fileName)
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