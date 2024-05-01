using System;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators
{
    public class CombiningTentacleServiceDecoratorFactory : ITentacleServiceDecoratorFactory
    {
        readonly List<ITentacleServiceDecoratorFactory> decoratorsInReverse;

        internal CombiningTentacleServiceDecoratorFactory(List<ITentacleServiceDecoratorFactory> decorators)
        {
            // Reversing the decorators means the first decorator in the given list will be called first.
            decoratorsInReverse = new List<ITentacleServiceDecoratorFactory>(decorators);
            decoratorsInReverse.Reverse();
        }

        public IAsyncClientScriptService Decorate(IAsyncClientScriptService service)
        {
            foreach (var decoratorFactory in decoratorsInReverse)
            {
                service = decoratorFactory.Decorate(service);
            }

            return service;
        }

        public IAsyncClientScriptServiceV2 Decorate(IAsyncClientScriptServiceV2 service)
        {
            foreach (var decoratorFactory in decoratorsInReverse)
            {
                service = decoratorFactory.Decorate(service);
            }
            return service;
        }

        public IAsyncClientFileTransferService Decorate(IAsyncClientFileTransferService service)
        {
            foreach (var decoratorFactory in decoratorsInReverse)
            {
                service = decoratorFactory.Decorate(service);
            }
            return service;
        }

        public IAsyncClientCapabilitiesServiceV2 Decorate(IAsyncClientCapabilitiesServiceV2 service)
        {
            foreach (var decoratorFactory in decoratorsInReverse)
            {
                service = decoratorFactory.Decorate(service);
            }
            return service;
        }

        public IAsyncClientKubernetesScriptServiceV1Alpha Decorate(IAsyncClientKubernetesScriptServiceV1Alpha service)
        {
            foreach (var decoratorFactory in decoratorsInReverse)
            {
                service = decoratorFactory.Decorate(service);
            }
            return service;
        }
    }
}