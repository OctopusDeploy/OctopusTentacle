using System;
using System.Net.Security;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Tests.Communications
{
    [TestFixture]
    public class ServerCertificateTrustConfirmationFixture
    {
        const string Thumbprint = "76B1C4A6F1E2B3C4D5E6F708192A3B4C5D6E7F80";

        static readonly Uri ServerAddress = new("wss://octopus.example.com/OctopusComms");

        ISystemLog log;
        IPrompt prompt;
        ServerCertificateTrustConfirmation confirmation;

        [SetUp]
        public void BeforeEachTest()
        {
            log = Substitute.For<ISystemLog>();
            prompt = Substitute.For<IPrompt>();
            confirmation = new ServerCertificateTrustConfirmation(log, prompt);
        }

        static OctopusServerCommunicationsCheckResult Validated() => new(Thumbprint, SslPolicyErrors.None);

        static OctopusServerCommunicationsCheckResult NotValidated(SslPolicyErrors errors = SslPolicyErrors.RemoteCertificateChainErrors) => new(Thumbprint, errors);

        [Test]
        public void ACertificateThatPassedValidationIsTrustedWithoutAskingTheOperator()
        {
            confirmation.Invoking(c => c.EnsureCertificateIsTrusted(ServerAddress, Validated(), null))
                .Should()
                .NotThrow();

            prompt.DidNotReceiveWithAnyArgs().Confirm(default!, default!);
            _ = prompt.DidNotReceive().CanPrompt;
        }

        [Test]
        public void ACertificateThatFailedValidationIsTrustedWithoutPromptingWhenItsThumbprintWasSuppliedUpFront()
        {
            confirmation.Invoking(c => c.EnsureCertificateIsTrusted(ServerAddress, NotValidated(), Thumbprint))
                .Should()
                .NotThrow();

            prompt.DidNotReceiveWithAnyArgs().Confirm(default!, default!);
            _ = prompt.DidNotReceive().CanPrompt;
        }

        [TestCase("76b1c4a6f1e2b3c4d5e6f708192a3b4c5d6e7f80", TestName = "DifferentCasing")]
        [TestCase("  76B1C4A6F1E2B3C4D5E6F708192A3B4C5D6E7F80  ", TestName = "SurroundingWhitespace")]
        public void ASuppliedThumbprintIsMatchedLeniently(string suppliedThumbprint)
        {
            confirmation.Invoking(c => c.EnsureCertificateIsTrusted(ServerAddress, NotValidated(), suppliedThumbprint))
                .Should()
                .NotThrow();

            prompt.DidNotReceiveWithAnyArgs().Confirm(default!, default!);
        }

        [Test]
        public void ACertificateIsRejectedWhenItsThumbprintDoesNotMatchTheOneSupplied()
        {
            prompt.CanPrompt.Returns(true);
            prompt.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            const string someOtherThumbprint = "0123456789ABCDEF0123456789ABCDEF01234567";

            confirmation.Invoking(c => c.EnsureCertificateIsTrusted(ServerAddress, NotValidated(), someOtherThumbprint))
                .Should()
                .Throw<ControlledFailureException>()
                .Where(e => e.Message.Contains(Thumbprint), "the operator needs to see what was actually presented")
                .Where(e => e.Message.Contains(someOtherThumbprint), "and what they asked for");

            prompt.DidNotReceiveWithAnyArgs().Confirm(default!, default!);
        }

        [Test]
        public void AValidCertificateIsStillRejectedWhenItsThumbprintDoesNotMatchTheOneSupplied()
        {
            confirmation.Invoking(c => c.EnsureCertificateIsTrusted(ServerAddress, Validated(), "0123456789ABCDEF0123456789ABCDEF01234567"))
                .Should()
                .Throw<ControlledFailureException>();
        }

        [Test]
        public void ACertificateThatFailedValidationIsRejectedWhenThereIsNobodyToAsk()
        {
            prompt.CanPrompt.Returns(false);

            confirmation.Invoking(c => c.EnsureCertificateIsTrusted(ServerAddress, NotValidated(), null))
                .Should()
                .Throw<ControlledFailureException>()
                .Where(e => e.Message.Contains(Thumbprint), "the operator needs to know which thumbprint to verify")
                .Where(e => e.Message.Contains("--server-web-socket-thumbprint"), "the message has to say how to proceed deliberately")
                .Where(e => e.Message.Contains("not the Octopus Server's Tentacle Communications certificate"),
                    "the websocket endpoint's certificate is configured in IIS/HTTP.SYS/a reverse proxy, so sending the operator to the thumbprint in the Octopus Server UI would send them to a value that can never match");

            prompt.DidNotReceiveWithAnyArgs().Confirm(default!, default!);
        }

        [Test]
        public void ACertificateThatFailedValidationIsTrustedWhenTheOperatorConfirmsIt()
        {
            prompt.CanPrompt.Returns(true);
            prompt.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            confirmation.Invoking(c => c.EnsureCertificateIsTrusted(ServerAddress, NotValidated(), null))
                .Should()
                .NotThrow();

            prompt.Received().Confirm(
                Arg.Is<string>(m => m.Contains(Thumbprint) && m.Contains("websockets certificate")),
                Arg.Any<string>());
        }

        [Test]
        public void ACertificateThatFailedValidationIsRejectedWhenTheOperatorDeclinesIt()
        {
            prompt.CanPrompt.Returns(true);
            prompt.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

            confirmation.Invoking(c => c.EnsureCertificateIsTrusted(ServerAddress, NotValidated(), null))
                .Should()
                .Throw<ControlledFailureException>();
        }

        [TestCase(SslPolicyErrors.RemoteCertificateChainErrors)]
        [TestCase(SslPolicyErrors.RemoteCertificateNameMismatch)]
        [TestCase(SslPolicyErrors.RemoteCertificateNotAvailable)]
        [TestCase(SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch)]
        public void EveryKindOfValidationFailureRequiresConfirmation(SslPolicyErrors errors)
        {
            prompt.CanPrompt.Returns(true);
            prompt.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

            confirmation.Invoking(c => c.EnsureCertificateIsTrusted(ServerAddress, NotValidated(errors), null))
                .Should()
                .Throw<ControlledFailureException>();
        }
    }
}
