using System;
using System.Linq;
using Autofac;
using Autofac.Core;
using FluentAssertions;
using Halibut.ServiceModel;
using NUnit.Framework;
using Octopus.Tentacle.Communications;

namespace Octopus.Tentacle.Tests.Communications
{
    public class AutofacServiceFactoryFixture
    {
        private readonly IAutofacServiceSource simpleSource = new KnownServiceSource(typeof(SimpleService));
        private readonly IAutofacServiceSource pieSource = new KnownServiceSource(typeof(PieService));
        private readonly IAutofacServiceSource emptySource = new KnownServiceSource();
        private readonly IAutofacServiceSource nullSource = new KnownServiceSource();
        private readonly IAutofacServiceSource invalidInterfaceSource = new KnownServiceSource(typeof(ISimpleService));
        private readonly IAutofacServiceSource invalidClassSource = new KnownServiceSource(typeof(PlainClass));

        [Test]
        public void Resolved_WithNoSources_WorksAsExpected()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();

            var container = builder.Build();

            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(0);
        }

        [Test]
        public void Resolved_WithSingleSource_CanCreateServices()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterInstance(simpleSource).AsImplementedInterfaces();

            var container = builder.Build();

            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(1);
            factory.RegisteredServiceTypes.Select(x => x.Name).Should().Contain(nameof(ISimpleService));

            var service = factory.CreateService(nameof(ISimpleService));
            var greeting = (service.Service as ISimpleService)?.Greet("Fred");
            greeting.Should().Be("Hello Fred!");
        }

        [Test]
        public void Resolved_WithMultipleSources_CanCreateServices()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterInstance(simpleSource).AsImplementedInterfaces();
            builder.RegisterInstance(pieSource).AsImplementedInterfaces();

            var container = builder.Build();

            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(2);
            var types = factory.RegisteredServiceTypes.Select(x => x.Name).ToList();
            types.Should().Contain(nameof(ISimpleService));
            types.Should().Contain(nameof(IPieService));

            var service = factory.CreateService(nameof(ISimpleService));
            var greeting = (service.Service as ISimpleService)?.Greet("Fred");
            greeting.Should().Be("Hello Fred!");

            var pieService = factory.CreateService(nameof(IPieService));
            var pie = (pieService.Service as IPieService)?.GetPie();
            pie.Should().Be(3.14159f);
        }

        [Test]
        public void Resolved_WithNullSource_DoesNotThrow()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterInstance(nullSource).AsImplementedInterfaces();
            var container = builder.Build();

            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(0);
        }

        [Test]
        public void Resolved_WithEmptySource_DoesNotThrow()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterInstance(emptySource).AsImplementedInterfaces();
            var container = builder.Build();

            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(0);
        }

        [Test]
        public void Resolved_WithInvalidInterfaceSource_ThrowsExpectedException()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterInstance(invalidInterfaceSource).AsImplementedInterfaces();
            var container = builder.Build();

            try
            {
                container.Resolve<IServiceFactory>();
            }
            catch (Exception ex)
            {
                ex.Should().BeOfType<DependencyResolutionException>();
                var baseEx = ex.GetBaseException();
                baseEx.Should().BeOfType<InvalidServiceTypeException>();
                (baseEx as InvalidServiceTypeException)?.InvalidType.Name.Should().Be(nameof(ISimpleService));
            }
        }

        [Test]
        public void Resolved_WithInvalidClassSource_ThrowsExpectedException()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterInstance(invalidClassSource).AsImplementedInterfaces();
            var container = builder.Build();

            try
            {
                container.Resolve<IServiceFactory>();
            }
            catch (Exception ex)
            {
                ex.Should().BeOfType<DependencyResolutionException>();
                var baseEx = ex.GetBaseException();
                baseEx.Should().BeOfType<InvalidServiceTypeException>();
                (baseEx as InvalidServiceTypeException)?.InvalidType.Name.Should().Be(nameof(PlainClass));
            }
        }

        [Test]
        public void CreateService_WithMissingService_ThrowsExpectedException()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterInstance(simpleSource).AsImplementedInterfaces();

            var container = builder.Build();

            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(1);

            const string missingService = "IAmNotHere";
            try
            {
                var _ = factory.CreateService(missingService);
            }
            catch (Exception ex)
            {
                ex.Should().BeOfType<UnknownServiceNameException>();
                (ex as UnknownServiceNameException)?.ServiceName.Should().Be(missingService);
            }
        }

        private interface IPieService
        {
            float GetPie();
        }

        private class PieService : IPieService
        {
            public float GetPie()
            {
                return 3.14159f;
            }
        }

        private interface ISimpleService
        {
            string Greet(string name);
        }

        private class SimpleService : ISimpleService
        {
            public string Greet(string name)
            {
                return $"Hello {name}!";
            }
        }

        // No interface, this will not be register-able
        private class PlainClass
        {
            // ReSharper disable once UnusedMember.Local
            public string Greet(string name)
            {
                return $"Hello {name}!";
            }
        }
    }
}