using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.ServiceModel;
using System.Text;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Model;
using Microsoft.WindowsAzure.ServiceManagement;
using Microsoft.WindowsAzure.StorageClient;
using Octopus.Shared.Activities;

namespace Octopus.Shared.Integration.Azure
{
    public class AzurePackageUploader : IAzurePackageUploader
    {
        const string OctopusPackagesContainerName = "octopuspackages";

        public Uri Upload(SubscriptionData subscription, string packageFile, string uploadedFileName, IActivityLog log, CancellationToken cancellation)
        {
            log.Debug("Connecting to Azure blob storage");

            var binding = new WebHttpBinding();
            binding.CloseTimeout = TimeSpan.FromSeconds(30);
            binding.OpenTimeout = TimeSpan.FromSeconds(30);
            binding.ReceiveTimeout = TimeSpan.FromMinutes(30);
            binding.SendTimeout = TimeSpan.FromMinutes(30);
            binding.ReaderQuotas.MaxStringContentLength = 1048576;
            binding.ReaderQuotas.MaxBytesPerRead = 131072;
            binding.Security.Mode = WebHttpSecurityMode.Transport;
            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Certificate;
            
            var client = new ServiceManagementClient(binding, new Uri(subscription.ServiceEndpoint), subscription.Certificate, ServiceManagementClientOptions.DefaultOptions);
            var storageKeys = client.Service.GetStorageKeys(subscription.SubscriptionId, subscription.CurrentStorageAccount);
            var storageAccount = new CloudStorageAccount(new StorageCredentialsAccountAndKey(subscription.CurrentStorageAccount, storageKeys.StorageServiceKeys.Primary), true);

            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(OctopusPackagesContainerName);
            container.CreateIfNotExist();

            var permission = container.GetPermissions();
            permission.PublicAccess = BlobContainerPublicAccessType.Off;
            container.SetPermissions(permission);

            var fileInfo = new FileInfo(packageFile);

            var blob = container.GetBlockBlobReference(uploadedFileName);
            
            log.Debug("Uploading package to blob storage...");

            using (var fileReader = fileInfo.OpenRead())
            {
                var blocklist = new List<string>();

                long uploadedSoFar = 0;

                var data = new byte[128 * 1024];
                var id = 1;

                while (true)
                {
                    id++;

                    cancellation.ThrowIfCancellationRequested();

                    var read = fileReader.Read(data, 0, data.Length);
                    if (read == 0)
                    {
                        blob.PutBlockList(blocklist);
                        break;
                    }

                    var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(id.ToString(CultureInfo.InvariantCulture).PadLeft(30, '0')));
                    blob.PutBlock(blockId, new MemoryStream(data, 0, read, true), null);
                    blocklist.Add(blockId);

                    uploadedSoFar += read;

                    log.OverwritePrevious().InfoFormat("Uploaded: {0} of {1} ({2:n2}%)", uploadedSoFar.ToFileSizeString(), fileInfo.Length.ToFileSizeString(), (uploadedSoFar / (double)fileInfo.Length * 100.00));
                }
            }

            log.OverwritePrevious().Info("Package upload complete");
            return blob.Uri;
        }
    }
}