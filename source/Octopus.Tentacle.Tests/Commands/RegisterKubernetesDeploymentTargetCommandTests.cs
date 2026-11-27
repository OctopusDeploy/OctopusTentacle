using System;
using System.Linq;
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
    public class RegisterKubernetesDeploymentTargetCommandTests
    {
        [Test]
        public void ShouldRegisterListeningTentacle()
        {
            var registration = new RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation>("3.0");
            var command = CommandBuilder.RegisterKubernetesDeploymentTarget(registration);

            command.StartWith("--env=Development",
                  "--server=http://localhost",
                  "--name=MyMachine",
                  "--publicHostName=mymachine.test",
                  "--apiKey=ABC123",
                  "--force",
                  "--proxy=Proxy",
                  "--role=app-server",
                  "--role=web-server",
                  "--tenant=Tenant1",
                  "--tenantTag=CustomerType/VIP",
                  "--default-namespace=myspace");

            Assert.That(registration.Operation.Environments.Single(), Is.EqualTo("Development"));
            Assert.That(registration.Operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(registration.Operation.TentacleHostname, Is.EqualTo("mymachine.test"));
            Assert.That(registration.Operation.TentaclePort, Is.EqualTo(90210));
            Assert.That(registration.Operation.TentacleThumbprint, Is.EqualTo(registration.Certificate.Thumbprint));
            Assert.That(registration.Operation.AllowOverwrite, Is.True);
            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentaclePassive));
            Assert.That(registration.Operation.MachinePolicy, Is.Null);
            Assert.That(registration.Operation.Roles.First(), Is.EqualTo("app-server"));
            Assert.That(registration.Operation.Roles.Last(), Is.EqualTo("web-server"));
            Assert.That(registration.Operation.SubscriptionId, Is.Null);
            Assert.That(registration.Operation.TenantTags.Single(), Is.EqualTo("CustomerType/VIP"));
            Assert.That(registration.Operation.Tenants.Single(), Is.EqualTo("Tenant1"));
            Assert.That(registration.Operation.ProxyName, Is.EqualTo("Proxy"));
            Assert.That(registration.Operation.DefaultNamespace, Is.EqualTo("myspace"));

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address == null &&
                                                        x.CommunicationStyle == CommunicationStyle.TentaclePassive &&
                                                        x.Thumbprint == registration.ServerThumbprint));
            
            registration.Configuration.Received().SetIsRegistered();
            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }

        [Test]
        public void ShouldRegisterPollingTentacle()
        {
            var registration = new RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation>("3.0");
            var command = CommandBuilder.RegisterKubernetesDeploymentTarget(registration);

            AssertPollingTentacleRegistered(registration, command, "https://localhost:10943/", "--server-comms-port=10943");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverDefaultPort()
        {
            var registration = new RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation>("3.0");
            var command = CommandBuilder.RegisterKubernetesDeploymentTarget(registration);

            AssertPollingTentacleRegistered(registration, command, "https://localhost:10943/");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverCustomCommsPort()
        {
            var registration = new RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation>("3.0");
            var command = CommandBuilder.RegisterKubernetesDeploymentTarget(registration);

            AssertPollingTentacleRegistered(registration, command, "https://localhost:123/", "--server-comms-port=123");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverCustomCommsAddressAndServerCommsPort()
        {
            var registration = new RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation>("3.0");
            var command = CommandBuilder.RegisterKubernetesDeploymentTarget(registration);

            AssertPollingTentacleRegistered(registration, command, 
                "https://polling.localhost:456/",
                "--server-comms-address=https://polling.localhost:123/",
                "--server-comms-port=456");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverCustomCommsAddressWithPort()
        {
            var registration = new RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation>("3.0");
            var command = CommandBuilder.RegisterKubernetesDeploymentTarget(registration);

            AssertPollingTentacleRegistered(registration, command, 
                "https://polling.localhost:123/",
                "--server-comms-address=https://polling.localhost:123/");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverCustomCommsAddressWithDefaultHttpsPort()
        {
            var registration = new RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation>("3.0");
            var command = CommandBuilder.RegisterKubernetesDeploymentTarget(registration);

            AssertPollingTentacleRegistered(registration, command, 
                "https://polling.localhost/",
                "--server-comms-address=https://polling.localhost/");
        }

        static void AssertPollingTentacleRegistered(RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation> registration, ICommand command, string expectedServerAddress, params string[] additionalArgs)
        {
            var args = new []
            {
                "--env=Development",
                "--server=http://localhost",
                "--name=MyMachine",
                "--publicHostName=mymachine.test",
                "--apiKey=ABC123",
                "--force",
                "--role=app-server",
                "--role=web-server",
                "--tenant=Tenant1",
                "--tenantTag=CustomerType/VIP",
                "--comms-style=TentacleActive",
                "--default-namespace=test-namespace"
            };

            args = args.Concat(additionalArgs).ToArray();

            command.StartWith(args);

            Assert.That(registration.Operation.Environments.Single(), Is.EqualTo("Development"));
            Assert.That(registration.Operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(registration.Operation.TentacleHostname, Is.Empty);
            Assert.That(registration.Operation.TentaclePort, Is.EqualTo(0));
            Assert.That(registration.Operation.TentacleThumbprint, Is.EqualTo(registration.Certificate.Thumbprint));
            Assert.That(registration.Operation.AllowOverwrite, Is.True);
            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(registration.Operation.MachinePolicy, Is.Null);
            Assert.That(registration.Operation.Roles.First(), Is.EqualTo("app-server"));
            Assert.That(registration.Operation.Roles.Last(), Is.EqualTo("web-server"));
            registration.Operation.SubscriptionId.ToString().Should().StartWith("poll://");
            Assert.That(registration.Operation.TenantTags.Single(), Is.EqualTo("CustomerType/VIP"));
            Assert.That(registration.Operation.Tenants.Single(), Is.EqualTo("Tenant1"));
            Assert.That(registration.Operation.DefaultNamespace, Is.EqualTo("test-namespace"));

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == expectedServerAddress &&
                                                   x.SubscriptionId == registration.Operation.SubscriptionId.ToString() &&
                                                   x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                                                   x.Thumbprint == registration.ServerThumbprint));

            registration.Configuration.Received().SetIsRegistered();
            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }

        [Test]
        public void ShouldReuseSubscriptionIdForPollingTentacleIfReRegistering()
        {
            var registration = new RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation>("3.0");
            var command = CommandBuilder.RegisterKubernetesDeploymentTarget(registration);

            var subscriptionId = "poll://xz6h25sh28shx52/";
            var octopusServerConfiguration = new OctopusServerConfiguration(registration.ServerThumbprint)
            {
                SubscriptionId = subscriptionId,
                CommunicationStyle = CommunicationStyle.TentacleActive,
                Address = new Uri("https://localhost:10943/")
            };
            registration.Configuration.TrustedOctopusServers.Returns(new[] {octopusServerConfiguration});

            command.StartWith("--env=Development",
                  "--server=http://localhost",
                  "--name=MyMachine",
                  "--apiKey=ABC123",
                  "--force",
                  "--role=app-server",
                  "--comms-style=TentacleActive",
                  "--server-comms-port=10943");

            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(registration.Operation.SubscriptionId.ToString(), Is.EqualTo(subscriptionId));

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == "https://localhost:10943/" &&
                                                   x.SubscriptionId == subscriptionId &&
                                                   x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                                                   x.Thumbprint == registration.ServerThumbprint));

            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }

        [Test]
        public void DoesntReuseSubscriptionIdForPollingTentacleIfNewRegistration()
        {
            var registration = new RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation>("3.0");
            var command = CommandBuilder.RegisterKubernetesDeploymentTarget(registration);

            var subscriptionId = "poll://xz6h25sh28shx52/";
            var octopusServerConfiguration = new OctopusServerConfiguration(registration.ServerThumbprint)
            {
                SubscriptionId = subscriptionId,
                CommunicationStyle = CommunicationStyle.TentacleActive,
                Address = new Uri("https://octopus.example.com:10943/")
            };
            registration.Configuration.TrustedOctopusServers.Returns(new[] {octopusServerConfiguration});

            command.StartWith("--env=Development",
                  "--server=http://localhost", //different server
                  "--name=MyMachine",
                  "--apiKey=ABC123",
                  "--force",
                  "--role=app-server",
                  "--comms-style=TentacleActive",
                  "--server-comms-port=10943");

            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(registration.Operation.SubscriptionId.ToString(), Is.Not.EqualTo(subscriptionId));

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == "https://localhost:10943/" &&
                                                   x.SubscriptionId != subscriptionId &&
                                                   x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                                                   x.Thumbprint == registration.ServerThumbprint));

            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }
        
        [Test]
        public void ShouldRegisterPollingTentacleWithCustomServerSubscriptionId()
        {
            var registration = new RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation>("3.0");
            var command = CommandBuilder.RegisterKubernetesDeploymentTarget(registration);

            var subscriptionId = "poll://xz6h25sh28shx52/";

            command.StartWith("--env=Development",
                "--server=http://localhost",
                "--name=MyMachine",
                "--apiKey=ABC123",
                "--force",
                "--role=app-server",
                "--comms-style=TentacleActive",
                "--server-comms-port=10943",
                $"--server-subscription-id={subscriptionId}"
                );

            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(registration.Operation.SubscriptionId.ToString(), Is.EqualTo(subscriptionId));

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == "https://localhost:10943/" &&
                    x.SubscriptionId == subscriptionId &&
                    x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                    x.Thumbprint == registration.ServerThumbprint));

            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }
        
        [Test]
        public void ShouldThrowExceptionWhenCustomServerSubscriptionIdDoesNotHaveCorrectScheme()
        {
            var registration = new RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation>("3.0");
            var command = CommandBuilder.RegisterKubernetesDeploymentTarget(registration);

            var ex = Assert.Throws<ControlledFailureException>(() => 
            command.StartWith("--env=Development",
                "--server=http://localhost",
                "--name=MyMachine",
                "--apiKey=ABC123",
                "--force",
                "--role=app-server",
                "--comms-style=TentacleActive",
                "--server-comms-port=10943",
                "--server-subscription-id=https://xz6h25sh28shx52/"
            ));
            ex.Message.Should().Be("The ServerSubscriptionId must start with poll://");
        }
        
        [Test]
        public void ShouldDoNothingWhenWorkerIsAlreadyRegistered()
        {
            var registration = new RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation>("3.0");
            var command = CommandBuilder.RegisterKubernetesDeploymentTarget(registration);

            registration.Configuration.IsRegistered.Returns(true);
            
            command.StartWith("--env=Development",
                "--server=http://localhost",
                "--name=MyMachine",
                "--publicHostName=mymachine.test",
                "--apiKey=ABC123",
                "--force",
                "--proxy=Proxy",
                "--role=app-server",
                "--role=web-server",
                "--tenant=Tenant1",
                "--tenantTag=CustomerType/VIP",
                "--default-namespace=myspace");
            
            registration.Operation.DidNotReceive().ExecuteAsync(Arg.Any<IOctopusSpaceAsyncRepository>());
            registration.Configuration.DidNotReceive().AddOrUpdateTrustedOctopusServer(Arg.Any<OctopusServerConfiguration>());
        }
    }
}