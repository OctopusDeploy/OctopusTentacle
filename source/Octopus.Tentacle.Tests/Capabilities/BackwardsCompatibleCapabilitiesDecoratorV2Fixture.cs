using FluentAssertions;
using Halibut;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.Transport.Protocol;
using NUnit.Framework;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Tests.Capabilities
{
    public class BackwardsCompatibleCapabilitiesDecoratorFixture
    {
        [Test]
        public void ShouldWrapHalibutServiceNotFoundExceptionToNoCapabilities()
        {
            var backwardsCompatibleCapabilitiesService = new ThrowsServiceNotFoundCapabilitiesService().WithBackwardsCompatability();
            var capabilities = backwardsCompatibleCapabilitiesService.GetCapabilities().SupportedCapabilities;
            capabilities.Should().Contain("IScriptService");
            capabilities.Should().Contain("IFileTransferService");
            capabilities.Count.Should().Be(2);
        }
        
        [Test]
        public void ShouldWrapTentacleSpecificServiceNotFoundExceptionToNoCapabilities()
        {
            var backwardsCompatibleCapabilitiesService = new ThrowsTentacleSpecificServiceNotFoundCapabilitiesService().WithBackwardsCompatability();
            var capabilities = backwardsCompatibleCapabilitiesService.GetCapabilities().SupportedCapabilities;
            capabilities.Should().Contain("IScriptService");
            capabilities.Should().Contain("IFileTransferService");
            capabilities.Count.Should().Be(2);
        }

        public class ThrowsServiceNotFoundCapabilitiesService : ICapabilitiesServiceV2
        {
            public CapabilitiesResponseV2 GetCapabilities()
            {
                throw new ServiceNotFoundHalibutClientException("Nope", "Can't find it");
            }
        }
        
        public class ThrowsTentacleSpecificServiceNotFoundCapabilitiesService : ICapabilitiesServiceV2
        {
            public CapabilitiesResponseV2 GetCapabilities()
            {
                // This is what an old tentacle would return.
                throw new HalibutClientException(@"Error: ICapabilitiesServiceV2 has not been registered through an IAutofacServiceSource",
@"Octopus.Tentacle.Communications.UnknownServiceNameException: Error: ICapabilitiesServiceV2 has not been registered through an IAutofacServiceSource
   at Octopus.Tentacle.Communications.AutofacServiceFactory.CreateService(String serviceName) in /opt/buildagent/work/639265b01610d682/source/Octopus.Tentacle/Communications/AutofacServiceFactory.cs:line 56
   at Halibut.ServiceModel.ServiceInvoker.Invoke(RequestMessage requestMessage)
   at Halibut.HalibutRuntime.HandleIncomingRequest(RequestMessage request)
   at Halibut.Transport.Protocol.MessageExchangeProtocol.InvokeAndWrapAnyExceptions(RequestMessage request, Func`2 incomingRequestProcessor)");
            }
        }
    }
}