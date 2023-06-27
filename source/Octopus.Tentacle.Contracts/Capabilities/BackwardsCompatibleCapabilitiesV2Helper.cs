using Halibut.Exceptions;

namespace Octopus.Tentacle.Contracts.Capabilities
{
    public class BackwardsCompatibleCapabilitiesV2Helper
    {
        private static readonly string NoMatchingServiceOrMethodHalibutClientExceptionTypeFullName;
        private static readonly string MethodNotFoundHalibutClientExceptionTypeFullName;
        private static readonly string ServiceNotFoundHalibutClientExceptionTypeFullName;
        private static readonly string AmbiguousMethodMatchHalibutClientExceptionTypeFullName;

        static BackwardsCompatibleCapabilitiesV2Helper()
        {
            NoMatchingServiceOrMethodHalibutClientExceptionTypeFullName = typeof(NoMatchingServiceOrMethodHalibutClientException).FullName!;
            MethodNotFoundHalibutClientExceptionTypeFullName = typeof(MethodNotFoundHalibutClientException).FullName!;
            ServiceNotFoundHalibutClientExceptionTypeFullName = typeof(ServiceNotFoundHalibutClientException).FullName!;
            AmbiguousMethodMatchHalibutClientExceptionTypeFullName = typeof(AmbiguousMethodMatchHalibutClientException).FullName!;
        }

        public static bool ExceptionTypeLooksLikeTheServiceWasNotFound(string typeFullName)
        {
            return typeFullName == NoMatchingServiceOrMethodHalibutClientExceptionTypeFullName ||
                typeFullName == MethodNotFoundHalibutClientExceptionTypeFullName ||
                typeFullName == ServiceNotFoundHalibutClientExceptionTypeFullName ||
                typeFullName == AmbiguousMethodMatchHalibutClientExceptionTypeFullName;
        }

        public static bool ExceptionMessageLooksLikeTheServiceWasNotFound(string exceptionMessage)
        {
            // More recent versions of tentacle threw this exception.
            if (exceptionMessage.Contains("Octopus.Tentacle.Communications.UnknownServiceNameException")) return true;

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
            if (exceptionMessage.Contains("CreateService("))
            {
                // Return
                if (exceptionMessage.Contains("The Tentacle service is shutting down and cannot process this request.")
                    || exceptionMessage.Contains("ObjectDisposedException"))
                {
                    return false;
                }

                return true;
            }

            if (exceptionMessage.Contains("NotRegistered")) return true;

            if (exceptionMessage.Contains(nameof(ICapabilitiesServiceV2))) return true;

            return false;
        }
    }
}
