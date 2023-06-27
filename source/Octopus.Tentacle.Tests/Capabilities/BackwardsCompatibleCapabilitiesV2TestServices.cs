using Halibut;
using Halibut.Exceptions;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Tests.Capabilities
{
    internal class BackwardsCompatibleCapabilitiesV2TestServices
    {
        internal const string OldTentacleMissingCapabilitiesServiceMessage = @"Error: ICapabilitiesServiceV2 has not been registered through an IAutofacServiceSource";

        internal const string OldTentacleMissingCapabilitiesServiceServiceException = @"Octopus.Tentacle.Communications.UnknownServiceNameException: Error: ICapabilitiesServiceV2 has not been registered through an IAutofacServiceSource
   at Octopus.Tentacle.Communications.AutofacServiceFactory.CreateService(String serviceName) in /opt/buildagent/work/639265b01610d682/source/Octopus.Tentacle/Communications/AutofacServiceFactory.cs:line 56
   at Halibut.ServiceModel.ServiceInvoker.Invoke(RequestMessage requestMessage)
   at Halibut.HalibutRuntime.HandleIncomingRequest(RequestMessage request)
   at Halibut.Transport.Protocol.MessageExchangeProtocol.InvokeAndWrapAnyExceptions(RequestMessage request, Func`2 incomingRequestProcessor)";

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
                throw new HalibutClientException(OldTentacleMissingCapabilitiesServiceMessage, OldTentacleMissingCapabilitiesServiceServiceException);
            }
        }
    }
}
