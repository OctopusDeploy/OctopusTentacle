using System;
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
            Assert.Throws<ArgumentException>(() =>
            {
                _ = new KnownService(typeof(PlainClass), typeof(IPlainInterface));
            });
        }

        [Test]
        public void CannotConstructWhenImplementationTypeIsAnInterface()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = new KnownService(typeof(IPlainInterface), typeof(IPlainInterface));
            });
        }

        [Test]
        public void CannotConstructWhenImplementationTypeIsAnAbstractClass()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = new KnownService(typeof(AbstractClass), typeof(IPlainInterface));
            });
        }

        [Test]
        public void CannotConstructWhenContractTypeIsNotAnInterface()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = new KnownService(typeof(ValidClass), typeof(PlainClass));
            });
        }
    }

    class ValidClass : IPlainInterface
    {
    }

    class PlainClass
    {
    }

    abstract class AbstractClass
    {
    }

    interface IPlainInterface
    {
    }
}