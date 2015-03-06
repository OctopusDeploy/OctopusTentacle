using System;
using System.Collections.Generic;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Endpoints
{
    /// <summary>
    /// Contains the information necessary to communicate with an Azure Cloud Service
    /// </summary>
    public class CloudServiceEndpoint : AgentlessEndpoint, IEndpointWithAccount
    {
        [Obsolete("Serialization constructor")]
        public CloudServiceEndpoint() : this(new Dictionary<string, Variable>()) { }

        public CloudServiceEndpoint(IDictionary<string, Variable> raw)
            : base(raw)
        {
        }

        public string SubscriptionId { get { return GetEndpointProperty<string>("SubscriptionId"); } set { SetEndpointProperty("SubscriptionId", value); } }
        public string CloudServiceName { get { return GetEndpointProperty<string>("CloudServiceName"); } set { SetEndpointProperty("CloudServiceName", value); } }
        public string StorageAccountName { get { return GetEndpointProperty<string>("StorageAccountName"); } set { SetEndpointProperty("StorageAccountName", value); } }
        public string Slot { get { return GetEndpointProperty<string>("Slot"); } set { SetEndpointProperty("Slot", value); } }
        public bool SwapIfPossible { get { return GetEndpointProperty<bool>("SwapIfPossible"); } set { SetEndpointProperty("SwapIfPossible", value); } }
        public bool UseCurrentInstanceCount { get { return GetEndpointProperty<bool>("UseCurrentInstanceCount"); } set { SetEndpointProperty("UseCurrentInstanceCount", value); } }
        public string CertificateBytes { get { return GetEndpointProperty<string>("CertificateBytes"); } set { SetEndpointProperty("CertificateBytes", value, isSensitive: true); } }
        public string CertificateThumbprint { get { return GetEndpointProperty<string>("CertificateThumbprint"); } set { SetEndpointProperty("CertificateThumbprint", value); } }
        public string ManagementEndpoint { get { return GetEndpointProperty<string>("ManagementEndpoint"); } set { SetEndpointProperty("ManagementEndpoint", value); } }
        public string AccountId { get { return GetEndpointProperty<string>("AccountId"); } set { SetEndpointProperty("AccountId", value); } }

        public override string ToString()
        {
            return CloudServiceName;
        }
    }
}