using System;
using FluentAssertions;
using Halibut;
using Halibut.Exceptions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Tests.Capabilities
{
    public class BackwardsCompatibleCapabilitiesV2HelperFixture
    {
        [Test]
        public void ExceptionTypeLooksLikeTheServiceWasNotFound_For_NoMatchingServiceOrMethodHalibutClientException()
        {
            var exception = new NoMatchingServiceOrMethodHalibutClientException("Nope");
            var result = BackwardsCompatibleCapabilitiesV2Helper.ExceptionTypeLooksLikeTheServiceWasNotFound(exception.GetType().FullName!);
            result.Should().BeTrue();
        }

        [Test]
        public void ExceptionTypeLooksLikeTheServiceWasNotFound_For_MethodNotFoundHalibutClientException()
        {
            var exception = new MethodNotFoundHalibutClientException("Nope");
            var result = BackwardsCompatibleCapabilitiesV2Helper.ExceptionTypeLooksLikeTheServiceWasNotFound(exception.GetType().FullName!);
            result.Should().BeTrue();
        }

        [Test]
        public void ExceptionTypeLooksLikeTheServiceWasNotFound_For_ServiceNotFoundHalibutClientException()
        {
            var exception = new ServiceNotFoundHalibutClientException("Nope");
            var result = BackwardsCompatibleCapabilitiesV2Helper.ExceptionTypeLooksLikeTheServiceWasNotFound(exception.GetType().FullName!);
            result.Should().BeTrue();
        }

        [Test]
        public void ExceptionTypeLooksLikeTheServiceWasNotFound_For_AmbiguousMethodMatchHalibutClientException()
        {
            var exception = new AmbiguousMethodMatchHalibutClientException("Nope");
            var result = BackwardsCompatibleCapabilitiesV2Helper.ExceptionTypeLooksLikeTheServiceWasNotFound(exception.GetType().FullName!);
            result.Should().BeTrue();
        }

        [Test]
        public void ExceptionTypeDoesNotLookLikeTheServiceWasNotFound_For_Exception()
        {
            var exception = new Exception("Nope");
            var result = BackwardsCompatibleCapabilitiesV2Helper.ExceptionTypeLooksLikeTheServiceWasNotFound(exception.GetType().FullName!);
            result.Should().BeFalse();
        }

        [Test]
        public void ExceptionTypeDoesNotLookLikeTheServiceWasNotFound_For_HalibutClientException()
        {
            var exception = new HalibutClientException("Nope");
            var result = BackwardsCompatibleCapabilitiesV2Helper.ExceptionTypeLooksLikeTheServiceWasNotFound(exception.GetType().FullName!);
            result.Should().BeFalse();
        }

        [Test]
        public void ExceptionMessageLooksLikeTheServiceWasNotFound()
        {
            // This is what an old tentacle would return.
            var exception = new HalibutClientException(BackwardsCompatibleCapabilitiesV2TestServices.OldTentacleMissingCapabilitiesServiceMessage,
                BackwardsCompatibleCapabilitiesV2TestServices.OldTentacleMissingCapabilitiesServiceServiceException);

            var result = BackwardsCompatibleCapabilitiesV2Helper.ExceptionMessageLooksLikeTheServiceWasNotFound(exception.Message);
            result.Should().BeTrue();
        }

        [Test]
        public void ExceptionMessageDoesNotLooksLikeTheServiceWasNotFound()
        {
            var exception = new HalibutClientException("An Error", "An Error Occurred");

            var result = BackwardsCompatibleCapabilitiesV2Helper.ExceptionMessageLooksLikeTheServiceWasNotFound(exception.Message);
            result.Should().BeFalse();
        }
    }
}