using System;
using System.Threading;
using Microsoft.WindowsAzure.Management.Model;
using Octopus.Platform.Diagnostics;

namespace Octopus.Shared.Integration.Azure
{
    public interface IAzurePackageUploader
    {
        Uri Upload(SubscriptionData subscription, string packageFile, string uploadedFileName, ILog log, CancellationToken cancellation);
    }
}