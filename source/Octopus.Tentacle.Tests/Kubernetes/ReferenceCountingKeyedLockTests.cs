using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Nito.AsyncEx;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Kubernetes.Synchronisation;
using Octopus.Tentacle.Kubernetes.Synchronisation.Internal;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class ReferenceCountingKeyedLockTests
    {
        CancellationTokenSource testCancellationTokenSource;
        Dictionary<int, int> referenceCountIntercepts;

        [SetUp]
        public void SetUp()
        {
            testCancellationTokenSource = new (TimeSpan.FromSeconds(10));
            referenceCountIntercepts = new();
        }

        [TearDown]
        public void TearDown()
        {
            testCancellationTokenSource.Dispose();
        }
        
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(20)]
        public async Task MultipleThreadsLockWithSameKey_OnDisposeCalledOnceForEachThread(int numThreads)
        {
            // Arrange
            var interceptingReleaserFactory = Substitute.For<ISemaphoreSlimReleaserFactory<SemaphoreSlimWithReferenceCount>>();

            interceptingReleaserFactory.Create(Arg.Any<SemaphoreSlimWithReferenceCount>(), Arg.Any<Action>())
                .Returns(callInfo =>
                {
                    var semaphoreSlimWithReferenceCount = callInfo.Arg<SemaphoreSlimWithReferenceCount>();
                    var onDispose = callInfo.Arg<Action>();
                    return new SemaphoreSlimReleaser<SemaphoreSlimWithReferenceCount>(
                        semaphoreSlimWithReferenceCount, () =>
                        {
                            onDispose();
                            RecordCallback(semaphoreSlimWithReferenceCount);
                        }
                    );
                });
            
            var keyedLock = new ReferenceCountingKeyedLock<ScriptTicket>(interceptingReleaserFactory);

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
            var interceptingReleaserFactory = Substitute.For<ISemaphoreSlimReleaserFactory<SemaphoreSlimWithReferenceCount>>();

            interceptingReleaserFactory.Create(Arg.Any<SemaphoreSlimWithReferenceCount>(), Arg.Any<Action>())
                .Returns(callInfo =>
                {
                    var semaphoreSlimWithReferenceCount = callInfo.Arg<SemaphoreSlimWithReferenceCount>();
                    var onDispose = callInfo.Arg<Action>();
                    return new SemaphoreSlimReleaser<SemaphoreSlimWithReferenceCount>(
                        semaphoreSlimWithReferenceCount, () =>
                        {
                            onDispose();
                            RecordCallback(semaphoreSlimWithReferenceCount);
                        }
                    );
                });
            
            var keyedLock = new ReferenceCountingKeyedLock<ScriptTicket>(interceptingReleaserFactory);

            // Act
            var tasks = new []
            {
                Task.Run(async () => await SimulateWork(keyedLock, "banana", testCancellationTokenSource.Token)),
                Task.Run(async () => await SimulateWork(keyedLock, "pineapple", testCancellationTokenSource.Token)),
                Task.Run(async () => await SimulateWork(keyedLock, "banana", testCancellationTokenSource.Token)),
            };
            await tasks.WhenAll();
            
            // Assert
            var expected = new Dictionary<int, int>
            {
                { 0, 2 },   // banana, pineapple
                { 1, 1 },   // banana
            };
            referenceCountIntercepts.Should().BeEquivalentTo(expected);
        }

        async Task SimulateWork(IKeyedLock<ScriptTicket> keyedLock, string taskId, CancellationToken cancellationToken)
        {
            using (await keyedLock.LockAsync(new ScriptTicket(taskId), testCancellationTokenSource.Token))
            {
                var rand = new Random();
                await Task.Delay(rand.Next(50, 100), cancellationToken);
            }
        }
        
        void RecordCallback(SemaphoreSlimWithReferenceCount semaphoreSlimWithReferenceCount)
        {
            if (!referenceCountIntercepts.TryAdd(semaphoreSlimWithReferenceCount.ReferenceCount, 1))
            {
                referenceCountIntercepts[semaphoreSlimWithReferenceCount.ReferenceCount] += 1;
            }
        }
    }
}