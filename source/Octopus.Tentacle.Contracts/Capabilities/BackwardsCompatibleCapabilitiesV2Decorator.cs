using System;
using System.Collections.Generic;
using Halibut;
using Halibut.Exceptions;

namespace Octopus.Tentacle.Contracts.Capabilities
{
    public class BackwardsCompatibleCapabilitiesV2Decorator : ICapabilitiesServiceV2
    {
        private readonly ICapabilitiesServiceV2 inner;

        public BackwardsCompatibleCapabilitiesV2Decorator(ICapabilitiesServiceV2 inner)
        {
            this.inner = inner;
        }

        public CapabilitiesResponseV2 GetCapabilities()
        {
            try
            {
                return inner.GetCapabilities();
            }
            catch (NoMatchingServiceOrMethodHalibutClientException)
            {
                return new CapabilitiesResponseV2(new List<string>() {nameof(IScriptService), nameof(IFileTransferService)});
            }
            catch (HalibutClientException e) when 
                (
                    ExceptionLooksLikeTheServiceWasNotFound(e))
            {
                return new CapabilitiesResponseV2(new List<string>() {nameof(IScriptService), nameof(IFileTransferService)});
            }
        }

        private static bool ExceptionLooksLikeTheServiceWasNotFound(HalibutClientException e)
        {

            if (e.Message.Contains("Octopus.Tentacle.Communications.UnknownServiceNameException")) return true;
            
            // Does it look like an exception from creating the service?
            //
            // When Halibut processes a request it will call `ServiceInvoker.Invoke` which finds the service and executes the request.
            // This here detects if the exception happened within that Invoke method, if it did and the stack trace is within the CreateService method
            // Then either:
            //    - The service could not be found.
            //    - The service could not be created
            if(e.Message.Contains("Halibut.ServiceModel.ServiceInvoker.Invoke(RequestMessage requestMessage)") 
               && e.Message.Contains("CreateService(String serviceName)"))
            {
                if (e.Message.Contains("The Tentacle service is shutting down and cannot process this request.")
                    || e.Message.Contains("ObjectDisposedException"))
                {
                    return false;
                }
                // Assume any inability to create or find the service with autofac means it does not exist.
                if (e.Message.ToLower().Contains("autofac"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}