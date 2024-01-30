using NUnit.Framework;

namespace Octopus.Manager.Tentacle.Tests
{
    [TestFixture]
    public class ApplicationStartupSanityCheckFixture
    {
        [Test]
        public void ApplicationCanStartWithoutCrashing()
        {
            Assert.Fail("insta-fail");
        }
    }
}
