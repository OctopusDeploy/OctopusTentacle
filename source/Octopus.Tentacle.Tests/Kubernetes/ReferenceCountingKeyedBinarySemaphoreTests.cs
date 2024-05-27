using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Nito.AsyncEx;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Kubernetes.Synchronisation;
using Octopus.Tentacle.Kubernetes.Synchronisation.Internal;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class ReferenceCountingKeyedBinarySemaphoreTests
    {
        CancellationTokenSource testCancellationTokenSource;
        Dictionary<int, int> referenceCountIntercepts;

        [SetUp]
        public void SetUp()
        {
            testCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            referenceCountIntercepts = new Dictionary<int, int>();
        }

        [TearDown]
        public void TearDown()
        {
            testCancellationTokenSource.Dispose();
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(20)]
        public async Task MultipleThreadsLockWithSameKey_OnDisposeCalledOnceForEachThread(int numThreads)
        {
            // Arrange
            var keyedLock = new TestableReferenceCountingKeyedBinarySemaphore<ScriptTicket>(RecordingReleaserFactory);

            // Act
            var tasks = Enumerable.Range(1, numThreads).Select(_ => Task.Run(async () => await SimulateWork(keyedLock, "foo", testCancellationTokenSource.Token)));
            await tasks.WhenAll();

            // Assert
            var expected = Enumerable.Range(0, numThreads).ToDictionary(x => x, _ => 1);
            referenceCountIntercepts.Should().BeEquivalentTo(expected);
        }

        [Test]
        public async Task ThreadsLockWithDifferentKeys_OnDisposeCalledWithCorrectReferenceCount()
        {
            // Arrange
            var keyedLock = new TestableReferenceCountingKeyedBinarySemaphore<ScriptTicket>(RecordingReleaserFactory);

            // Act
            var tasks = new[]
            {
                Task.Run(async () => await SimulateWork(keyedLock, "banana", testCancellationTokenSource.Token)),
                Task.Run(async () => await SimulateWork(keyedLock, "pineapple", testCancellationTokenSource.Token)),
                Task.Run(async () => await SimulateWork(keyedLock, "banana", testCancellationTokenSource.Token))
            };
            await tasks.WhenAll();

            // Assert
            var expected = new Dictionary<int, int>
            {
                { 0, 2 }, // banana, pineapple
                { 1, 1 } // banana
            };
            referenceCountIntercepts.Should().BeEquivalentTo(expected);
        }

        async Task SimulateWork(IKeyedSemaphore<ScriptTicket> keyedSemaphore, string taskId, CancellationToken cancellationToken)
        {
            using (await keyedSemaphore.WaitAsync(new ScriptTicket(taskId), testCancellationTokenSource.Token))
            {
                var rand = new Random();
                await Task.Delay(rand.Next(50, 100), cancellationToken);
            }
        }

        SemaphoreSlimReleaser<ReferenceCountingBinarySemaphoreSlim> RecordingReleaserFactory(ReferenceCountingBinarySemaphoreSlim referenceCountingBinarySemaphore, Action onDispose)
        {
            return new SemaphoreSlimReleaser<ReferenceCountingBinarySemaphoreSlim>(referenceCountingBinarySemaphore, InterceptedOnDispose);

            void InterceptedOnDispose()
            {
                onDispose();
                RecordCallback(referenceCountingBinarySemaphore);
            }
        }

        void RecordCallback(ReferenceCountingBinarySemaphoreSlim referenceCountingBinarySemaphoreSlim)
        {
            if (!TryAdd(referenceCountIntercepts, referenceCountingBinarySemaphoreSlim.ReferenceCount, 1)) referenceCountIntercepts[referenceCountingBinarySemaphoreSlim.ReferenceCount] += 1;
        }

        // The extension method Dictionary<TKey,TValue>.TryAdd(TKey, TValue) is not available in .NET Framework
        static bool TryAdd<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }
            
            if (!dictionary.ContainsKey(key))
            {
                dictionary.Add(key, value);
                return true;
            }

            return false;
        }

        class TestableReferenceCountingKeyedBinarySemaphore<TKey> : ReferenceCountingKeyedBinarySemaphore<TKey> where TKey : IEquatable<TKey>
        {
            public TestableReferenceCountingKeyedBinarySemaphore(CreateSemaphoreSlimReleaser releaserFactory) : base(releaserFactory)
            {
            }
        }
    }
}