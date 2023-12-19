using System;
using Halibut.Exceptions;

namespace Octopus.Tentacle.Client.Scripts
{
    public static class HalibutClientExceptionExtensionMethods
    {
        public static bool IsConnectingOrTransferringRequestCancelledException(this Exception ex)
        {
            return ex is ConnectingRequestCancelledException or TransferringRequestCancelledException;
        }
    }
}
