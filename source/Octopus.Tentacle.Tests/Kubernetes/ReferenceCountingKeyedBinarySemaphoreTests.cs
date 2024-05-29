using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes.Synchronisation.Internal;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    [Timeout(20000)] //Timeout test after 20 seconds
    public class ReferenceCountingKeyedBinarySemaphoreTests
    {
        [Test]
        public async Task WaitingOnTheSameKey_BlocksSuccessiveAttempts()
        {
            // Arrange
            var referenceCountingSemaphore = new ReferenceCountingKeyedBinarySemaphore<string>();
            var sharedKey = "SomeSharedKey";
            var periodToWaitForLock = TimeSpan.FromSeconds(2); // How long should the task wait to attempt to acquire the lock.
            
            // Block on the shared key
            await referenceCountingSemaphore.WaitAsync(sharedKey, CancellationToken.None);
            
            // An attempt to acquire the lock should block (and ultimately fail when we timeout the attempt)
            try
            {
                await referenceCountingSemaphore.WaitAsync(sharedKey, new CancellationTokenSource(periodToWaitForLock).Token);
                Assert.Fail("This acquisition should not have taken place");
            }
            catch (Exception ex)
            {
                ex.Should().BeOfType<OperationCanceledException>();
            }
        }
        
        [Test]
        public async Task WaitingOnTheDifferentKey_AllowsImmediateAcquisition()
        {
            // Arrange
            var referenceCountingSemaphore = new ReferenceCountingKeyedBinarySemaphore<string>();

            // Act
            await referenceCountingSemaphore.WaitAsync("firstKey", CancellationToken.None);
            var disposable = await referenceCountingSemaphore.WaitAsync("secondKey", CancellationToken.None);

            disposable.Should().NotBeNull();
        }
        

        [Test]
        public async Task WaitingOnTheSameKey_AllowsReAcquisitionAfterDisposal()
        {
            // Arrange
            var referenceCountingSemaphore = new ReferenceCountingKeyedBinarySemaphore<string>();
            var sharedKey = "SomeSharedKey";
            
            // Act
            var disposableLock1 = await referenceCountingSemaphore.WaitAsync(sharedKey, CancellationToken.None);
            disposableLock1.Dispose();
            
            // Assert
            await referenceCountingSemaphore.WaitAsync(sharedKey, CancellationToken.None);
        }
    }
}