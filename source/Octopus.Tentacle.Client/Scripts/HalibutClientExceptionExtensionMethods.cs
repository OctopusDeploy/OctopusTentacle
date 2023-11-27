using System;
using Halibut;

namespace Octopus.Tentacle.Client.Scripts
{
    public static class HalibutClientExceptionExtensionMethods
    {
        public static bool IsHalibutWrappedConnectingRequestCancelledException(this HalibutClientException? ex)
        {
            if (ex is null)
            {
                return false;
            }

            if (ex.Message.Contains("The Request was cancelled while Connecting"))
            {
                return true;
            }

            return false;
        }

        public static bool IsHalibutWrappedConnectingRequestCancelledException(this Exception ex)
        {
            return (ex as HalibutClientException).IsHalibutWrappedConnectingRequestCancelledException();
        }
    }
}
