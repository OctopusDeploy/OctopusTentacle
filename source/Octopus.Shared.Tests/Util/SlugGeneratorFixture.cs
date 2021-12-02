using System;
using System.Collections.Generic;
using NUnit.Framework;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    [TestFixture]
    public class SlugGeneratorFixture
    {
        [Test]
        public void ShouldNotReturnBlankWhenAllSpecialCharacters()
        {
            Assert.That(SlugGenerator.GenerateSlug("()", _ => false), Is.EqualTo("blue-ring"));
        }

        [Test]
        public void ShouldLowercaseAndDashifyWords()
        {
            var existing = new List<string>();
            Assert.That(SlugGenerator.GenerateSlug("Hello World!", existing.Contains), Is.EqualTo("hello-world"));
            Assert.That(SlugGenerator.GenerateSlug("Hello !!! World!", existing.Contains), Is.EqualTo("hello-world"));
            Assert.That(SlugGenerator.GenerateSlug("Green-.-|Bannana!", existing.Contains), Is.EqualTo("green-bannana"));
            Assert.That(SlugGenerator.GenerateSlug("Green//Bannana!", existing.Contains), Is.EqualTo("green-bannana"));
        }

        [Test]
        public void ShouldAppendUniqueIdentifier()
        {
            var existing = new List<string>();
            existing.Add("hello");
            existing.Add("hello-1");
            existing.Add("hello-2");
            existing.Add("hello-3");

            Assert.That(SlugGenerator.GenerateSlug("Hello!", existing.Contains), Is.EqualTo("hello-4"));
        }
    }
}
