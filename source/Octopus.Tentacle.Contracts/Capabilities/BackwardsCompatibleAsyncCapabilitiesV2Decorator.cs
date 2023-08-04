using System;
using System.Collections.Generic;
using Halibut;
using Halibut.Exceptions;

namespace Octopus.Tentacle.Contracts.Capabilities
{
    public class BackwardsCompatibleAsyncCapabilitiesV2Decorator : ICapabilitiesServiceV2
    {
        private readonly ICapabilitiesServiceV2 inner;

        public BackwardsCompatibleAsyncCapabilitiesV2Decorator(ICapabilitiesServiceV2 inner)
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

        static bool ExceptionLooksLikeTheServiceWasNotFound(HalibutClientException e)
        {
            return BackwardsCompatibleCapabilitiesV2Helper.ExceptionMessageLooksLikeTheServiceWasNotFound(e.Message);
        }
    }
}