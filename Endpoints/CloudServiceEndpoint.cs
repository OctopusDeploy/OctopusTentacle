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

        public string SubscriptionId { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }
        public string CloudServiceName { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }
        public string StorageAccountName { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }
        public string Slot { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }
        public bool SwapIfPossible { get { return GetEndpointProperty<bool>(); } set { SetEndpointProperty(value); } }
        public bool UseCurrentInstanceCount { get { return GetEndpointProperty<bool>(); } set { SetEndpointProperty(value); } }
        public string CertificateBytes { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value, isSensitive: true); } }
        public string CertificateThumbprint { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }
        public string ManagementEndpoint { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }
        public string AccountId { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }

        public override string ToString()
        {
            return CloudServiceName;
        }
    }
}