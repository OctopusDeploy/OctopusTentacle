using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Operations;
using Octopus.Client.Repositories.Async;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Commands.Support;

namespace Octopus.Tentacle.Tests.Commands
{
    public class RegisterWorkerCommandTests
    {
        [Test]
        public void ShouldRegisterListeningWorker()
        {
            var registration = new RegistrationSubstitutes<IRegisterWorkerOperation>("2018.4");
            var command = CommandBuilder.RegisterWorker(registration);

            command.StartWith("--workerpool=SomePool",
                "--server=http://localhost",
                "--name=MyMachine",
                "--publicHostName=mymachine.test",
                "--apiKey=ABC123",
                "--force",
                "--proxy=Proxy");

            Assert.That(registration.Operation.WorkerPools.Single(), Is.EqualTo("SomePool"));
            Assert.That(registration.Operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(registration.Operation.TentacleHostname, Is.EqualTo("mymachine.test"));
            Assert.That(registration.Operation.TentaclePort, Is.EqualTo(90210));
            Assert.That(registration.Operation.TentacleThumbprint, Is.EqualTo(registration.Certificate.Thumbprint));
            Assert.That(registration.Operation.AllowOverwrite, Is.True);
            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentaclePassive));
            Assert.That(registration.Operation.MachinePolicy, Is.Null);
            Assert.That(registration.Operation.SubscriptionId, Is.Null);
            Assert.That(registration.Operation.ProxyName, Is.EqualTo("Proxy"));

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address == null &&
                    x.CommunicationStyle == CommunicationStyle.TentaclePassive &&
                    x.Thumbprint == registration.ServerThumbprint));

            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }

        [Test]
        public void ShouldRegisterPollingWorker()
        {
            var registration = new RegistrationSubstitutes<IRegisterWorkerOperation>("2018.4");
            var command = CommandBuilder.RegisterWorker(registration);

            AssertPollingWorkerRegistered(registration, command, "https://localhost:10943/", "--server-comms-port=10943");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverDefaultPort()
        {
            var registration = new RegistrationSubstitutes<IRegisterWorkerOperation>("2018.4");
            var command = CommandBuilder.RegisterWorker(registration);

            AssertPollingWorkerRegistered(registration, command, "https://localhost:10943/");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverCustomCommsPort()
        {
            var registration = new RegistrationSubstitutes<IRegisterWorkerOperation>("2018.4");
            var command = CommandBuilder.RegisterWorker(registration);

            AssertPollingWorkerRegistered(registration, command, "https://localhost:123/", "--server-comms-port=123");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverCustomCommsAddressAndServerCommsPort()
        {
            var registration = new RegistrationSubstitutes<IRegisterWorkerOperation>("2018.4");
            var command = CommandBuilder.RegisterWorker(registration);

            AssertPollingWorkerRegistered(registration, command, 
                "https://polling.localhost:456/",
                "--server-comms-address=https://polling.localhost:123/",
                "--server-comms-port=456");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverCustomCommsAddressWithPort()
        {
            var registration = new RegistrationSubstitutes<IRegisterWorkerOperation>("2018.4");
            var command = CommandBuilder.RegisterWorker(registration);

            AssertPollingWorkerRegistered(registration, command, 
                "https://polling.localhost:123/",
                "--server-comms-address=https://polling.localhost:123/");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverCustomCommsAddressWithDefaultHttpsPort()
        {
            var registration = new RegistrationSubstitutes<IRegisterWorkerOperation>("2018.4");
            var command = CommandBuilder.RegisterWorker(registration);

            AssertPollingWorkerRegistered(registration, command, 
                "https://polling.localhost/",
                "--server-comms-address=https://polling.localhost/");
        }

        // no need for tests like ShouldReuseSubscriptionIdForPollingTentacleIfReRegistering
        // because it runs the same code for Workers and Deployment targets

        [Test]
        public void ShouldDefaultPollingConnectionCountWhenRegisteringAPollingWorker()
        {
            var registration = new RegistrationSubstitutes<IRegisterWorkerOperation>("2018.4");
            var command = CommandBuilder.RegisterWorker(registration);

            var expectedConnectionCount = RegisterWorkerCommandBase<IRegisterWorkerOperation>.DefaultPollingConnectionCountFor(Environment.ProcessorCount);

            AssertPollingWorkerRegistered(registration, command, "https://localhost:10943/");
            registration.Configuration.Received().SetPollingConnectionCount(expectedConnectionCount);
        }

        [TestCase(1, 5)]
        [TestCase(5, 5)]
        [TestCase(16, 16)]
        [TestCase(128, 128)]
        [TestCase(129, 128)]
        [TestCase(512, 128)]
        public void DefaultPollingConnectionCountScalesWithProcessorsWithinBounds(int processorCount, int expected)
        {
            RegisterWorkerCommandBase<IRegisterWorkerOperation>.DefaultPollingConnectionCountFor(processorCount).Should().Be(expected);
        }

        [Test]
        public void ShouldNotSetPollingConnectionCountWhenRegisteringAListeningWorker()
        {
            var registration = new RegistrationSubstitutes<IRegisterWorkerOperation>("2018.4");
            var command = CommandBuilder.RegisterWorker(registration);

            command.StartWith("--workerpool=SomePool",
                "--server=http://localhost",
                "--name=MyMachine",
                "--publicHostName=mymachine.test",
                "--apiKey=ABC123",
                "--force");

            registration.Configuration.DidNotReceive().SetPollingConnectionCount(Arg.Any<int>());
        }

        [Test]
        public void ShouldNotOverridePollingConnectionCountWhenAlreadyConfigured()
        {
            var registration = new RegistrationSubstitutes<IRegisterWorkerOperation>("2018.4");
            var command = CommandBuilder.RegisterWorker(registration);

            registration.Configuration.PollingConnectionCount.Returns(3);

            AssertPollingWorkerRegistered(registration, command, "https://localhost:10943/");

            registration.Configuration.DidNotReceive().SetPollingConnectionCount(Arg.Any<int>());
        }

        static void AssertPollingWorkerRegistered(RegistrationSubstitutes<IRegisterWorkerOperation> registration, ICommand command, string expectedServerAddress, params string[] additionalArgs)
        {
            var args = new []
            {
                "--workerpool=SomePool",
                "--server=http://localhost",
                "--name=MyMachine",
                "--publicHostName=mymachine.test",
                "--apiKey=ABC123",
                "--force",
                "--comms-style=TentacleActive",
            };

            args = args.Concat(additionalArgs).ToArray();

            command.StartWith(args);

            Assert.That(registration.Operation.WorkerPools.Single(), Is.EqualTo("SomePool"));
            Assert.That(registration.Operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(registration.Operation.TentacleHostname, Is.Empty);
            Assert.That(registration.Operation.TentaclePort, Is.EqualTo(0));
            Assert.That(registration.Operation.TentacleThumbprint, Is.EqualTo(registration.Certificate.Thumbprint));
            Assert.That(registration.Operation.AllowOverwrite, Is.True);
            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(registration.Operation.MachinePolicy, Is.Null);
            registration.Operation.SubscriptionId.ToString().Should().StartWith("poll://");

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == expectedServerAddress &&
                    x.SubscriptionId == registration.Operation.SubscriptionId.ToString() &&
                    x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                    x.Thumbprint == registration.ServerThumbprint));

            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }

    }
}