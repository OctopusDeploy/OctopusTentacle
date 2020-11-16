using System;
using NUnit.Framework;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    [TestFixture]
    public class EnumerableExtensionsFxture
    {
        [Test]
        public void ShouldBeMissing()
        {
            var subject = new[] { 1, 2, 3 };
            var actual = subject.Missing(4);
            Assert.That(actual, Is.True);
        }

        [Test]
        public void ShouldNotBeMissing()
        {
            var subject = new[] { 1, 2, 3 };
            var actual = subject.Missing(3);
            Assert.That(actual, Is.False);
        }
    }
}