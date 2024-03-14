using NSubstitute;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Tests.Builders
{
    class TentacleClientObserverBuilder
    {
        public ITentacleClientObserver Build()
        {
            return Substitute.For<ITentacleClientObserver>();
        }

        public static ITentacleClientObserver Default() => new TentacleClientObserverBuilder().Build();
    }
}