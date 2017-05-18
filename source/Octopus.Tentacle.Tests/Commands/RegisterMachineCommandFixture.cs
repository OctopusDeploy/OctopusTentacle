using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client;
using Octopus.Client.Operations;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Security;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class RegisterMachineCommandFixture : CommandFixture<RegisterMachineCommand>
    {
        ITentacleConfiguration configuration;
        ILog log;
        X509Certificate2 certificate;
        IRegisterMachineOperation operation;
        IOctopusServerChecker serverChecker;

        public void ShouldRegisterMachine()
        {
            configuration = Substitute.For<ITentacleConfiguration>();
            operation = Substitute.For<IRegisterMachineOperation>();
            serverChecker = Substitute.For<IOctopusServerChecker>();
            log = Substitute.For<ILog>();
            Command = new RegisterMachineCommand(new Lazy<IRegisterMachineOperation>(() => operation), new Lazy<ITentacleConfiguration>(() => configuration), log, Substitute.For<IApplicationInstanceSelector>(), new Lazy<IOctopusServerChecker>(() => serverChecker), new ProxyConfigParser());

            configuration.ServicesPortNumber.Returns(90210);
            certificate = new CertificateGenerator().GenerateNew("CN=Hello");
            configuration.TentacleCertificate.Returns(certificate);

            Start("-env=Development", "-server=http://localhost", "-name=MyMachine", "-publicHostName=mymachine.test", "--apiKey=ABC123", "-f");

            Assert.That(operation.EnvironmentNames.Single(), Is.EqualTo("Development"));
            Assert.That(operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(operation.TentacleHostname, Is.EqualTo("mymachine.test"));
            Assert.That(operation.TentaclePort, Is.EqualTo(90210));
            Assert.That(operation.TentacleThumbprint, Is.EqualTo(certificate.Thumbprint));

            operation.Received().Execute(Arg.Is<OctopusServerEndpoint>(c => c.ApiKey == "ABC123" && c.OctopusServer.ToString() == "http://localhost"));
        }
    }
}