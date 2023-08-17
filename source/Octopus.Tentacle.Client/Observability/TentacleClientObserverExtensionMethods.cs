using System;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Observability
{
    public static class TentacleClientObserverExtensionMethods
    {
        public static ITentacleClientObserver DecorateWithNonThrowingTentacleClientObserver(this ITentacleClientObserver tentacleClientObserver)
        {
            return new NonThrowingTentacleClientObserverDecorator(tentacleClientObserver);
        }
    }
}