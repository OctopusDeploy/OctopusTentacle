using System;
using System.Linq;
using Autofac;
using FluentAssertions;
using Halibut.ServiceModel;
using NUnit.Framework;
using Octopus.Shared.Communications;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Tests.Contracts
{
    public class ServiceFixture
    {
        class DummyScriptService : IScriptService
        {
            public ScriptTicket StartScript(StartScriptCommand command)
            {
                throw new NotImplementedException();
            }

            public ScriptStatusResponse GetStatus(ScriptStatusRequest request)
            {
                throw new NotImplementedException();
            }

            public ScriptStatusResponse CancelScript(CancelScriptCommand command)
            {
                throw new NotImplementedException();
            }

            public ScriptStatusResponse CompleteScript(CompleteScriptCommand command)
            {
                throw new NotImplementedException();
            }
        }
        
        readonly IAutofacServiceSource serviceSource = new KnownServiceSource(typeof(DummyScriptService));
        
        [Test]
        public void IScriptService_CanBeRegistered()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterInstance(serviceSource).AsImplementedInterfaces();
            
            var container = builder.Build();
            
            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(1);
            factory.RegisteredServiceTypes.Select(x => x.Name).Should().Contain(nameof(IScriptService));

            var service = factory.CreateService(nameof(IScriptService));
            Assert.Throws<NotImplementedException>(() => (service.Service as IScriptService)?.CancelScript(new CancelScriptCommand(new ScriptTicket("ticket"), 0)));
        }
    }
}