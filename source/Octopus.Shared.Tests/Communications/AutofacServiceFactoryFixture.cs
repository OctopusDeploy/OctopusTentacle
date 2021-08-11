using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using Autofac.Core;
using FluentAssertions;
using Halibut.ServiceModel;
using NUnit.Framework;
using Octopus.Shared.Communications;

namespace Octopus.Shared.Tests.Communications
{
    public class AutofacServiceFactoryFixture
    {
        [Test]
        public void NoSources_WorksAsExpected()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            
            var container = builder.Build();
            
            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(0);
        }
        
        [Test]
        public void SingleServiceSource_RegistersCorrectNamedTypes()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SimpleServiceSource>().AsImplementedInterfaces().SingleInstance();
            
            var container = builder.Build();
            
            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(1);
            factory.RegisteredServiceTypes.Select(x => x.Name).Should().Contain(nameof(ISimpleService));

            var service = factory.CreateService(nameof(ISimpleService));
            var greeting = (service.Service as ISimpleService)?.Greet("Fred");
            greeting.Should().Be("Hello Fred!");
        }

        [Test]
        public void MultipleServiceSources_RegisterCorrectTypes()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SimpleServiceSource>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<PieServiceSource>().AsImplementedInterfaces().SingleInstance();
            
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
        public void Source_ReturningNull_DoesNotThrow()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<NullServiceSource>().AsImplementedInterfaces().SingleInstance();
            var container = builder.Build();
            
            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(0);
        }
        
        [Test]
        public void Source_ReturningEmpty_DoesNotThrow()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<EmptyServiceSource>().AsImplementedInterfaces().SingleInstance();
            var container = builder.Build();
            
            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(0);
        }
        
        [Test]
        public void InvalidSource_ThrowsExpectedException()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<InvalidServiceSource>().AsImplementedInterfaces().SingleInstance();
            var container = builder.Build();

            Assert.Throws<DependencyResolutionException>(() => container.Resolve<IServiceFactory>());
        }
        
        public interface IPieService
        {
            float GetPie();
        }

        public class PieService : IPieService
        {
            public float GetPie()
            {
                return 3.14159f;
            }
        }
        
        public class PieServiceSource : IAutofacServiceSource
        {
            public IEnumerable<Type> GetServices()
            {
                yield return typeof(PieService);
            }
        }
        
        public interface ISimpleService
        {
            string Greet(string name);
        }

        public class SimpleService : ISimpleService
        {
            public string Greet(string name)
            {
                return $"Hello {name}!";
            }
        }

        public class SimpleServiceSource : IAutofacServiceSource
        {
            public IEnumerable<Type> GetServices()
            {
                yield return typeof(SimpleService);
            }
        }
        
        public class NullServiceSource : IAutofacServiceSource
        {
            public IEnumerable<Type> GetServices()
            {
                return null;
            }
        }
        
        public class EmptyServiceSource : IAutofacServiceSource
        {
            public IEnumerable<Type> GetServices()
            {
                return new Type[] {};
            }
        }
        
        public class InvalidServiceSource  : IAutofacServiceSource
        {
            public IEnumerable<Type> GetServices()
            {
                yield return typeof(ISimpleService);
            }
        }
    }
}