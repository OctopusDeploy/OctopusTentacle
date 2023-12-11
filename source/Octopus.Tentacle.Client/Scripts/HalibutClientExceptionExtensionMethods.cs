using System;
using Halibut;
using Halibut.Exceptions;

namespace Octopus.Tentacle.Client.Scripts
{
    public static class HalibutClientExceptionExtensionMethods
    {
        public static bool IsConnectingOrTransferringRequestCancelledException(this Exception ex)
        {
            return ex is ConnectingRequestCancelledException or TransferringRequestCancelledException ||
                ex.IsHalibutWrappedConnectingRequestCancelledException() ||
                ex.IsHalibutWrappedTransferringRequestCancelledException();
        }

        public static bool IsHalibutWrappedConnectingRequestCancelledException(this HalibutClientException? ex)
        {
            if (ex is null)
            {
                return false;
            }

            // TODO: Make this type safe
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

        public static bool IsHalibutWrappedTransferringRequestCancelledException(this HalibutClientException? ex)
        {
            if (ex is null)
            {
                return false;
            }

            // TODO: Make this type safe
            if (ex.Message.Contains("The Request was cancelled while Transferring"))
            {
                return true;
            }

            return false;
        }

        public static bool IsHalibutWrappedTransferringRequestCancelledException(this Exception ex)
        {
            return (ex as HalibutClientException).IsHalibutWrappedTransferringRequestCancelledException();
        }
    }
}
