using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Time;

namespace Octopus.Tentacle.Tests.Scripts
{
    [TestFixture]
    public class ExponentialBackoffFixture
    {
        readonly Foo foo = new Foo();

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 4)]
        [TestCase(4, 8)]
        public void BelowMaxDuration_ExponentiallyGrows(int retryAttempt, int expectedDuration)
        {
            ExponentialBackoff.GetDuration(retryAttempt, 20).Should().Be(expectedDuration);
        }
        
        [TestCase(6)]
        [TestCase(50)]
        [TestCase(1000000000)]
        public void AboveMaxDuration_CapsAtMax(int retryAttempt)
        {
            ExponentialBackoff.GetDuration(retryAttempt, 20).Should().Be(20);
        }
    }
}