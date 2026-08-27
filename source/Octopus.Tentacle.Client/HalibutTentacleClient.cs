using System;
using Halibut;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts.Capabilities;
using ITentacleClientObserver = Octopus.Tentacle.Contracts.Observability.ITentacleClientObserver;

namespace Octopus.Tentacle.Client
{
    public class HalibutTentacleClient : TentacleClient
    {
        public static void CacheServiceWasNotFoundResponseMessages(IHalibutRuntime halibutRuntime)
        {
            using var activity = ActivitySource.StartActivity($"{nameof(TentacleClient)}.{nameof(CacheServiceWasNotFoundResponseMessages)}");

            var innerHandler = halibutRuntime.OverrideErrorResponseMessageCaching;
            halibutRuntime.OverrideErrorResponseMessageCaching = response =>
            {
                if (BackwardsCompatibleCapabilitiesV2Helper.ExceptionTypeLooksLikeTheServiceWasNotFound(response.Error!.HalibutErrorType!) ||
                    BackwardsCompatibleCapabilitiesV2Helper.ExceptionMessageLooksLikeTheServiceWasNotFound(response.Error.Message))
                {
                    return true;
                }

                return innerHandler?.Invoke(response) ?? false;
            };
        }

        public HalibutTentacleClient(
            ServiceEndPoint serviceEndPoint,
            IHalibutRuntime halibutRuntime,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            ITentacleClientObserver tentacleClientObserver,
            TentacleClientOptions clientOptions
        ) : this(serviceEndPoint, halibutRuntime, scriptObserverBackOffStrategy, tentacleClientObserver, clientOptions, null)
        {
        }

        internal HalibutTentacleClient(
            ServiceEndPoint serviceEndPoint,
            IHalibutRuntime halibutRuntime,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            ITentacleClientObserver tentacleClientObserver,
            TentacleClientOptions clientOptions,
            ITentacleServiceDecoratorFactory? tentacleServicesDecoratorFactory)
            : base(new HalibutRpcActions(serviceEndPoint, halibutRuntime, tentacleServicesDecoratorFactory), scriptObserverBackOffStrategy, tentacleClientObserver, clientOptions)
        {
        }
    }
}