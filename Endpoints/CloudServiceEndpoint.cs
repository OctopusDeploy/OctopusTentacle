using System;
using Octopus.Client.Model;
using Octopus.Shared.Security;

namespace Octopus.Shared.Endpoints
{
    /// <summary>
    /// Contains the information necessary to communicate with an Azure Cloud Service
    /// </summary>
    public class CloudServiceEndpoint : Endpoint, IEndpointWithAccount
    {
        public override CommunicationStyle CommunicationStyle
        {
            get { return CommunicationStyle.CloudService; }
        }

        public string SubscriptionId { get; set; }
        public string CloudServiceName { get; set; }
        public string StorageAccountName { get; set; }
        public string Slot { get; set; }
        public bool SwapIfPossible { get; set; }
        public bool UseCurrentInstanceCount { get; set; }

        [Encrypted]
        public string CertificateBytes { get; set; }
        public string CertificateThumbprint { get; set; }
        public string ManagementEndpoint { get; set; }
        public string AccountId { get; set; }

        public override string ToString()
        {
            return CloudServiceName;
        }
    }
}