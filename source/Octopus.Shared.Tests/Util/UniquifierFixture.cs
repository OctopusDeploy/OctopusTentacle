using System;
using System.Collections.Generic;
using NUnit.Framework;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    public class UniquifierFixture
    {
        HashSet<string> inUse;

        [SetUp]
        public void SetUp()
        {
            inUse = new HashSet<string>();
        }

        [Test]
        public void ShouldUseFirstValue()
        {
            inUse.Clear();

            var unique = Uniquifier.UniquifyString("Hello", o => inUse.Contains(o));
            Assert.That(unique, Is.EqualTo("Hello"));
        }

        [Test]
        public void ShouldGenerateUniqueValue()
        {
            inUse.Add("Hello");

            var unique = Uniquifier.UniquifyString("Hello", o => inUse.Contains(o));
            Assert.That(unique, Is.EqualTo("Hello-1"));
        }

        [Test]
        public void ShouldGenerateUniqueValueUntilFound()
        {
            inUse.Add("Hello");
            inUse.Add("Hello-1");
            inUse.Add("Hello-2");
            inUse.Add("Hello-3");
            inUse.Add("Hello-4");
            inUse.Add("Hello-5");

            var unique = Uniquifier.UniquifyString("Hello", o => inUse.Contains(o));
            Assert.That(unique, Is.EqualTo("Hello-6"));
        }
    }
}