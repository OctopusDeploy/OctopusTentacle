using System;
using System.Linq;
using Autofac;
using FluentAssertions;
using Halibut;
using Halibut.ServiceModel;
using NUnit.Framework;
using Octopus.Shared.Communications;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Tests.Contracts
{
    public class FileTransferFixture
    {
        class DummyFileTransferService : IFileTransferService
        {
            public UploadResult UploadFile(string remotePath, DataStream upload)
            {
                throw new NotImplementedException();
            }

            public DataStream DownloadFile(string remotePath)
            {
                throw new NotImplementedException();
            }

            // If this method were on the interface it could not be registered
            // ReSharper disable once UnusedMember.Local
            object ImNotPublic()
            {
                return this;
            }
            
            // If this method were on the interface it could not be registered
            // ReSharper disable once UnusedMember.Local
            // ReSharper disable once InconsistentNaming
            public object IAmPublic()
            {
                return this;
            }
        }
        
        readonly IAutofacServiceSource serviceSource = new KnownServiceSource(typeof(DummyFileTransferService));
        
        [Test]
        public void IFileTransferService_CanBeRegistered()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<AutofacServiceFactory>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterInstance(serviceSource).AsImplementedInterfaces();
            
            var container = builder.Build();
            
            var factory = container.Resolve<IServiceFactory>();
            factory.RegisteredServiceTypes.Count.Should().Be(1);
            factory.RegisteredServiceTypes.Select(x => x.Name).Should().Contain(nameof(IFileTransferService));

            var service = factory.CreateService(nameof(IFileTransferService));
            Assert.Throws<NotImplementedException>(() => (service.Service as IFileTransferService)?.DownloadFile("path"));
        }
    }
}