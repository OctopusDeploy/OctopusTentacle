using System;
using System.Xml.Linq;
using Microsoft.WindowsAzure.Management.Model;

namespace Octopus.Shared.Integration.Azure
{
    public interface IAzureConfigurationRetriever
    {
        XDocument GetCurrentConfiguration(SubscriptionData subscription, string serviceName, string slot);
    }
}