using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.WindowsAzure.Management.Model;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Integration.Azure
{
    public class SubscriptionDataFactory
    {
        public static SubscriptionData CreateFromAzureStep(VariableDictionary variables, X509Certificate2 certificate)
        {
            var subscription = new SubscriptionData();
            subscription.ServiceEndpoint = "https://management.core.windows.net";
            subscription.SubscriptionId = variables.GetValue(SpecialVariables.Step.Azure.SubscriptionId);
            subscription.Certificate = certificate;
            subscription.CurrentStorageAccount = variables.GetValue(SpecialVariables.Step.Azure.StorageAccountName);
            
            var customEndpoint = variables.GetValue(SpecialVariables.Step.Azure.Endpoint);
            if (!string.IsNullOrWhiteSpace(customEndpoint))
            {
                subscription.ServiceEndpoint = customEndpoint;
            }

            return subscription;
        }
    }
}