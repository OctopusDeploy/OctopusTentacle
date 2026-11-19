using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ExpectedPollingClientVerifier : IPollingSubscriptionVerifier
    {
        readonly Uri expectedSubscriptionId;
        readonly string expectedThumbprint;

        public ExpectedPollingClientVerifier(Uri expectedSubscriptionId, string expectedThumbprint)
        {
            this.expectedSubscriptionId = expectedSubscriptionId;
            this.expectedThumbprint = expectedThumbprint;
        }

        public async Task<bool> IsExpectedClientAsync(Uri subscriptionId, string clientThumbprint, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            
            return subscriptionId == expectedSubscriptionId
                && string.Equals(clientThumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase);
        }
    }
}
