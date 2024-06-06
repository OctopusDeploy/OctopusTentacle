extern alias TaskScheduler;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    public class KubernetesEventMonitorFixture
    {
        [Test]
        public async Task NoEntriesAreSentToMetricsWhenEventListIsEmpty()
        {
            
        }

        [Test]
        public async Task NfsPodStartAndKillingEventsAreTrackedInMetrics()
        {
            
        }

        [Test]
        public async Task NfsWatchDogEventsAreTrackedInMetrics()
        {
            
        }

        [Test]
        public async Task EventsOlderThanMetricsTimestampCursorAreNotAddedToMetrics()
        {
            
        }
    }
}