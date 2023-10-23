using NUnit.Framework;
using Octopus.Tentacle.Communications;

namespace Octopus.Tentacle.Tests.Communications
{
    [TestFixture]
    public class KnownServiceFixture
    {
        [Test]
        public void CannotConstructWhenImplementationTypeHasNoInterfaces()
        {
            Assert.Throws<InvalidServiceTypeException>(() =>
            {
                _ = new KnownService(typeof(PlainClass), typeof(IPlainClass));
            });
        }


        [Test]
        public void CannotConstructWhenImplementationTypeIsAnInterface()
        {
            Assert.Throws<InvalidServiceTypeException>(() =>
            {
                _ = new KnownService(typeof(IPlainClass), typeof(IPlainClass));
            });
        }
    }

    class PlainClass
    { }

    interface IPlainClass
    {
    }
}