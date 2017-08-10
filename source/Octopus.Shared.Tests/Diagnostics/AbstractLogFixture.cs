using NUnit.Framework;
using Octopus.Shared.Tests.Support;

namespace Octopus.Shared.Tests.Diagnostics
{
    [TestFixture]
    public class AbstractLogFixture
    {
        [Test]
        public void InvalidFormatStringsDoNotResultInExceptions()
        {
            var log = new InMemoryLog();
            log.InfoFormat("I'm {0} this parameter: {1}", "missing");
            var text = log.GetLog();
            Assert.That(text, Is.StringContaining("I'm {0} this parameter: {1}"));
            Assert.That(text, Is.StringContaining("missing"));
            Assert.That(text, Is.StringContaining("=> Index"));
        }
    }
}