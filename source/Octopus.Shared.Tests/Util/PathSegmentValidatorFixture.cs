using NUnit.Framework;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    [TestFixture]
    public class PathSegmentValidatorFixture
    {
        [Test]
        [TestCase("Hello", true)]
        [TestCase("Hello A", true)]
        [TestCase("Hello world !", true)]
        [TestCase("Hello (world)", true)]
        [TestCase("H:\\ello (world)", false)]
        [TestCase("Hello\\world", false)]
        public void ShouldBeValid(string example, bool isValid)
        {
            var valid = PathSegmentValidator.IsValid(example);
            Assert.That(valid, Is.EqualTo(isValid));
        }
    }
}