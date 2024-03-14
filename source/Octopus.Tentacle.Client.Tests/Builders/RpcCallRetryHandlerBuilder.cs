using System;
using Octopus.Tentacle.Client.Retries;

namespace Octopus.Tentacle.Client.Tests.Builders
{
    class RpcCallRetryHandlerBuilder
    {
        public RpcCallRetryHandler Build()
        {
            return new RpcCallRetryHandler(TimeSpan.FromSeconds(5));
        }

        public static RpcCallRetryHandler Default() => new RpcCallRetryHandlerBuilder().Build();
    }
}