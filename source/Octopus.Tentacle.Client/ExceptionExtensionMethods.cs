using System;
using Halibut;
using Halibut.Exceptions;
using Halibut.Transport;

namespace Octopus.Tentacle.Client
{
    public static class ExceptionExtensionMethods
    {
        public static bool IsConnectionException(this Exception exception)
        {
            return exception is ConnectingRequestCancelledException 
                || exception is HalibutClientException {ConnectionState: ConnectionState.Connecting};
        }
    }
}