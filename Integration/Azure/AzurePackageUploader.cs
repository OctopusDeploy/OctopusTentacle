using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Model;
using Microsoft.WindowsAzure.ServiceManagement;
using Microsoft.WindowsAzure.StorageClient;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;

namespace Octopus.Shared.Integration.Azure
{
    public class AzurePackageUploader : IAzurePackageUploader
    {
        const string OctopusPackagesContainerName = "octopuspackages";

        public Uri Upload(SubscriptionData subscription, string packageFile, string uploadedFileName, ILog log, CancellationToken cancellation)
        {
            log.Verbose("Connecting to Azure blob storage");

            StorageService storageKeys;
            using (var client = new AzureClientFactory().CreateClient(subscription))
            {
                storageKeys = client.Service.GetStorageKeys(subscription.SubscriptionId, subscription.CurrentStorageAccount);
            }

            var storageAccount = new CloudStorageAccount(new StorageCredentialsAccountAndKey(subscription.CurrentStorageAccount, storageKeys.StorageServiceKeys.Primary), true);

            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(OctopusPackagesContainerName);
            container.CreateIfNotExist();

            var permission = container.GetPermissions();
            permission.PublicAccess = BlobContainerPublicAccessType.Off;
            container.SetPermissions(permission);

            var fileInfo = new FileInfo(packageFile);

            var packageBlob = GetUniqueBlobName(uploadedFileName, log, fileInfo, container);
            if (packageBlob.Exists())
            {
                log.Verbose("A blob named " + packageBlob.Name + " already exists with the same length, so it will be used instead of uploading the new package.");
                return packageBlob.Uri;
            }

            UploadBlobInChunks(log, cancellation, fileInfo, packageBlob);

            // OverwritePrevious
            log.Info("Package upload complete");
            return packageBlob.Uri;
        }

        static CloudBlockBlob GetUniqueBlobName(string uploadedFileName, ILog log, FileInfo fileInfo, CloudBlobContainer container)
        {
            var length = fileInfo.Length;
            var packageBlob = Uniquifier.UniquifyUntil(
                uploadedFileName,
                container.GetBlockBlobReference,
                blob =>
                {
                    if (blob.Exists() && blob.Properties.Length != length)
                    {
                        log.Verbose("A blob named " + blob.Name + " already exists but has a different length.");
                        return true;
                    }

                    return false;
                });

            return packageBlob;
        }

        static void UploadBlobInChunks(ILog log, CancellationToken cancellation, FileInfo fileInfo, CloudBlockBlob packageBlob)
        {
            log.Verbose("Uploading the package to blob storage...");

            using (var fileReader = fileInfo.OpenRead())
            {
                packageBlob.UploadFromStream(fileReader);

            }
        }
    }
}