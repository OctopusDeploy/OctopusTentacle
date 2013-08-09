using System;
using System.Threading;
using Microsoft.WindowsAzure.Management.Model;
using Octopus.Shared.Orchestration.Logging;

namespace Octopus.Shared.Integration.Azure
{
    public interface IAzurePackageUploader
    {
        Uri Upload(SubscriptionData subscription, string packageFile, string uploadedFileName, ITrace log, CancellationToken cancellation);
    }
}