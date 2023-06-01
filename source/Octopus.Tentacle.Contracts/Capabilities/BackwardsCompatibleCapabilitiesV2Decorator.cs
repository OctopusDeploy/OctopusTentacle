using System;
using System.Collections.Generic;
using Halibut;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;

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
            return WithBackwardsCompatability(inner.GetCapabilities);
        }

        internal static CapabilitiesResponseV2 WithBackwardsCompatability(Func<CapabilitiesResponseV2> inner)
        {
            try
            {
                return inner();
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

            // More recent versions of tentacle threw this exception.
            if (e.Message.Contains("Octopus.Tentacle.Communications.UnknownServiceNameException")) return true;

            // All services in Halibut are constructed via an implementation of the IServiceFactory
            // Halibut calls the CreateService() method on that interface. History[1] of that
            // interface shows its type and package have been
            // - Halibut.ServiceModel.CreateService(string serviceName)
            // - Halibut.Server.ServiceModel.CreateService(string serviceName)
            // - Halibut.Server.ServiceModel.CreateService(Type serviceType)
            // 
            // The CreateService method has existed since Jan 10 2013
            //
            // 1. https://github.com/OctopusDeploy/Halibut/commits/main/source/Halibut/ServiceModel/IServiceFactory.cs
            //
            // This here assumes that in tentacle any failure in CreateService means that the service could not be found. 
            if(e.Message.Contains("CreateService("))
            {
                // Return 
                if (e.Message.Contains("The Tentacle service is shutting down and cannot process this request.")
                    || e.Message.Contains("ObjectDisposedException"))
                {
                    return false;
                }

                return true;
            }

            if (e.Message.Contains("NotRegistered")) return true;

            if (e.Message.Contains(nameof(ICapabilitiesServiceV2))) return true;

            return false;
        }
    }
}