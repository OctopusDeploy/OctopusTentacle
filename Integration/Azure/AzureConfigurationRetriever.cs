using System;
using System.Xml.Linq;
using Microsoft.WindowsAzure.Management.Model;
using Microsoft.WindowsAzure.Management.Utilities;
using Microsoft.WindowsAzure.ServiceManagement;

namespace Octopus.Shared.Integration.Azure
{
    public class AzureConfigurationRetriever : IAzureConfigurationRetriever
    {
        public XDocument GetCurrentConfiguration(SubscriptionData subscription, string serviceName, string slot)
        {
            using (var client = new AzureClientFactory().CreateClient(subscription))
            {
                var deployment = client.Service.GetDeploymentBySlot(subscription.SubscriptionId, serviceName, slot);
                if (deployment != null)
                {
                    var xml = ServiceManagementHelper.DecodeFromBase64String(deployment.Configuration);
                    return XDocument.Parse(xml);
                }
            }

            return null;
        }
    }
}