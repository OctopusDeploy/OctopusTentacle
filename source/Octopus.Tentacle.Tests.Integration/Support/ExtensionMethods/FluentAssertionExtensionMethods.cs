using System;
using FluentAssertions;
using FluentAssertions.Primitives;
using Halibut.Exceptions;

namespace Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods
{
    public static class FluentAssertionExtensionMethods
    {
        public static AndConstraint<ObjectAssertions> BeRequestCancelledException(this ObjectAssertions should, RpcCallStage rpcCallStage)
        {
            if (rpcCallStage == RpcCallStage.Connecting)
            {
                return should.Match(x => x is ConnectingRequestCancelledException);
            }

            return should.Match(x => x is TransferringRequestCancelledException);
        }
        
        public static AndConstraint<ObjectAssertions> BeScriptExecutionCancelledException(this ObjectAssertions should)
        {
            return should.Match(x => x is OperationCanceledException && x.As<OperationCanceledException>().Message.Contains("Script execution was cancelled"));
        }
    }
}
