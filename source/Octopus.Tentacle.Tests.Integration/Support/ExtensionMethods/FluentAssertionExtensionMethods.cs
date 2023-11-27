using System;
using FluentAssertions;
using FluentAssertions.Primitives;
using Halibut;
using Halibut.Exceptions;

namespace Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods
{
    public static class FluentAssertionExtensionMethods
    {
        public static AndConstraint<ObjectAssertions> BeRequestCancelledException(this ObjectAssertions should, RpcCallStage rpcCallStage)
        {
            if (rpcCallStage == RpcCallStage.Connecting)
            {
                return should.Match(x => x is ConnectingRequestCancelledException || IsHalibutWrappedConnectingRequestCancelledException(x as HalibutClientException));
            }

            return should.Match(x => x is TransferringRequestCancelledException || IsHalibutWrappedTransferringRequestCancelledException(x as HalibutClientException));
        }

        static bool IsHalibutWrappedConnectingRequestCancelledException(HalibutClientException? halibutClientException)
        {
            return halibutClientException != null && halibutClientException.Message.Contains("The Request was cancelled while Connecting");
        }

        static bool IsHalibutWrappedTransferringRequestCancelledException(HalibutClientException? halibutClientException)
        {
            return halibutClientException != null && halibutClientException.Message.Contains("The Request was cancelled while Transferring");
        }

        public static AndConstraint<ObjectAssertions> BeScriptExecutionCancelledException(this ObjectAssertions should)
        {
            return should.Match(x => x is OperationCanceledException && x.As<OperationCanceledException>().Message.Contains("Script execution was cancelled"));
        }
    }
}
