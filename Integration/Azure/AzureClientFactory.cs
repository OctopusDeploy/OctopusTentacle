using System;
using System.ServiceModel;
using Microsoft.WindowsAzure.Management.Model;
using Microsoft.WindowsAzure.ServiceManagement;

namespace Octopus.Shared.Integration.Azure
{
    public interface IAzureClient : IDisposable
    {
        IServiceManagement Service { get; }
    }

    public class AzureClientFactory
    {
        public IAzureClient CreateClient(SubscriptionData subscription)
        {
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
            return new ClientWrapper(client);
        }

        class ClientWrapper : IAzureClient
        {
            readonly ServiceManagementClient client;

            public ClientWrapper(ServiceManagementClient client)
            {
                this.client = client;
            }

            public void Dispose()
            {
                client.Dispose();
            }

            public IServiceManagement Service { get { return client.Service; } }
        }
    }
}