using System;
using System.Threading;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    [TestFixture]
    public class ThrottleFixture
    {
        [Test]
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void ValidSizesOnlyPlease(int size)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Throttle("name", size));
        }

        [Test]
        public void CanEnterThrottle()
        {
            var throttle = new Throttle("test-throttle", size: 10);
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            using (var letMeIn = throttle.Wait(cts.Token))
            {
                letMeIn.Should().NotBeNull();
            }
        }

        [Test]
        public void ShouldProvideSensibleDebugInfo()
        {
            var throttle = new Throttle("test-throttle", size: 10);
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            using (var letMeIn = throttle.Wait(cts.Token))
            {
                throttle.ToString().Should().Be("test-throttle:1/10");
            }
        }

        [Test]
        public void ShouldPreventGoingOverCapacity_EvenOnTheSameThread()
        {
            var throttle = new Throttle("test-throttle", size: 1);
            using (var letMeIn = throttle.Wait(CancellationToken.None))
            {
                letMeIn.Should().NotBeNull();
                using (var secondCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
                {
                    Assert.Throws<OperationCanceledException>(() => throttle.Wait(secondCancellation.Token));
                }
            }
        }
    }
}