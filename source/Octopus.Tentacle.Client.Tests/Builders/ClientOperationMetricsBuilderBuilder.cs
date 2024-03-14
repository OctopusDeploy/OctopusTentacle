using System;
using Octopus.Tentacle.Client.Observability;

namespace Octopus.Tentacle.Client.Tests.Builders
{
    class ClientOperationMetricsBuilderBuilder
    {
        DateTimeOffset? start;

        public ClientOperationMetricsBuilderBuilder WithStart(DateTimeOffset start)
        {
            this.start = start;
            return this;
        }

        public ClientOperationMetricsBuilder Build() => new(start.GetValueOrDefault(DateTimeOffset.Now));

        public static ClientOperationMetricsBuilder Default() => new ClientOperationMetricsBuilderBuilder().Build();
    }
}