using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using FluentAssertions;
using Halibut.ServiceModel;
using NUnit.Framework;
using Octopus.Tentacle.Communications;

namespace Octopus.Tentacle.Tests.Communications
{
    public class AutofacServiceFactoryFixture
    {
        readonly IAutofacServiceSource simpleSource = new KnownServiceSource(new KnownService(typeof(SimpleService), typeof(ISimpleService)));
        readonly IAutofacServiceSource asyncSimpleSource = new KnownServiceSource(new KnownService(typeof(AsyncSimpleService), typeof(ISimpleService)));
        readonly IAutofacServiceSource pieSource = new KnownServiceSource(new KnownService(typeof(PieService), typeof(IPieService)));
        readonly IAutofacServiceSource emptySource = new KnownServiceSource();
        readonly IAutofacServiceSource nullSource = new KnownServiceSource();

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
            builder.RegisterType<SimpleService>().AsSelf().SingleInstance();
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
        public async Task Resolved_WithSingleSource_CanCreateAsyncServices()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<AsyncSimpleService>().AsSelf().SingleInstance();
            builder.RegisterInstance(asyncSimpleSource).AsImplementedInterfaces();

            var container = builder.Build();

            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(1);
            factory.RegisteredServiceTypes.Select(x => x.Name).Should().Contain(nameof(ISimpleService));

            var service = factory.CreateService(nameof(ISimpleService));
            var greeting = await (service.Service as IAsyncSimpleService)!.GreetAsync("Fred", CancellationToken.None);
            greeting.Should().Be("Hello Fred!");
        }

        [Test]
        public void Resolved_WithPerLifetimeScopeSingleSource_EachCreateServiceReturnsSameInstance()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SimpleService>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterInstance(simpleSource).AsImplementedInterfaces();

            var container = builder.Build();

            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(1);
            factory.RegisteredServiceTypes.Select(x => x.Name).Should().Contain(nameof(ISimpleService));

            var service1 = factory.CreateService(nameof(ISimpleService));
            var service2 = factory.CreateService(nameof(ISimpleService));

            service1.Service.Should().BeSameAs(service2.Service);
        }

        [Test]
        public void Resolved_WithSingleInstanceSingleSource_EachCreateServiceReturnsSameInstance()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SimpleService>().AsSelf().SingleInstance();
            builder.RegisterInstance(simpleSource).AsImplementedInterfaces();

            var container = builder.Build();

            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(1);
            factory.RegisteredServiceTypes.Select(x => x.Name).Should().Contain(nameof(ISimpleService));

            var service1 = factory.CreateService(nameof(ISimpleService));
            var service2 = factory.CreateService(nameof(ISimpleService));

            service1.Service.Should().BeSameAs(service2.Service);
        }

        [Test]
        public void Resolved_WithMultipleSources_CanCreateServices()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SimpleService>().AsSelf().SingleInstance();
            builder.RegisterInstance(simpleSource).AsImplementedInterfaces();
            builder.RegisterType<PieService>().AsSelf().SingleInstance();
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

        interface IPieService
        {
            float GetPie();
        }

        class PieService : IPieService
        {
            public float GetPie()
            {
                return 3.14159f;
            }
        }

        interface ISimpleService
        {
            string Greet(string name);
        }

        class SimpleService : ISimpleService
        {
            public string Greet(string name)
            {
                return $"Hello {name}!";
            }
        }

        interface IAsyncSimpleService
        {
            Task<string> GreetAsync(string name, CancellationToken cancellationToken);
        }

        class AsyncSimpleService: IAsyncSimpleService
        {
            public async Task<string> GreetAsync(string name, CancellationToken cancellationToken)
            {
                await Task.CompletedTask;
                return $"Hello {name}!";
            }
        }
    }
}